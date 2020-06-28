using System.Linq;
using Carter;
using Carter.ModelBinding;
using Carter.Request;
using Carter.Response;
using MongoDB.Bson;
using Recetron.Api.Interfaces;
using Recetron.Core.Models;

namespace Recetron.Api
{
  public class RecipeModule : CarterModule
  {
    public RecipeModule(IAuthService _auth, IRecipeService _recipes) : base("/api/recipes")
    {
      this.Before = ctx => ModuleHelpers.VerifyJwt(ctx, _auth);
      Get("", async (req, res) =>
      {
        var user = await _auth.ExtractUserAsync(ModuleHelpers.ExtractTokenStr(req.HttpContext));
        if (user == null)
        {
          res.StatusCode = 422;
          await res.Negotiate(new ErrorResponse { Message = "Missing User from Token" });
          return;
        }
        var _page = req.Query.FirstOrDefault(f => f.Key == "page").Value.FirstOrDefault();
        var _limit = req.Query.FirstOrDefault(f => f.Key == "limit").Value.FirstOrDefault();
        var (page, limit) = ModuleHelpers.GetPagination(_page, _limit);
        var recipes = await _recipes.FindByUser(user.Id ?? ObjectId.Empty, page, limit);
        await res.Negotiate(recipes);
        return;
      });
      
      Get("{id:string}", async (req, res) =>
      {
        var strId = req.RouteValues.As<string>("id");
        var recipeId = ObjectId.Parse(strId);
        var recipe = await _recipes.FindOne(recipeId);
        var user = await _auth.ExtractUserAsync(ModuleHelpers.ExtractTokenStr(req.HttpContext));
        if (recipe.UserId != user?.Id)
        {
          res.StatusCode = 403;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "You don't have access to this recipe",
            }
          );
          return;
        }

        await res.Negotiate(recipe);
      });

      Post("", async (req, res) =>
      {
        var user = await _auth.ExtractUserAsync(ModuleHelpers.ExtractTokenStr(req.HttpContext));
        if (user?.Id == null)
        {
          res.StatusCode = 422;
          await res.Negotiate(new ErrorResponse { Message = "Missing User from Token" });
          return;
        }

        var result =  await req.BindAndValidate<Recipe>();
        if (!result.ValidationResult.IsValid)
        {
          res.StatusCode = 400;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "Failed Validation",
              Errors = result.ValidationResult.GetFormattedErrors()
            }
          );
          return;
        }

        var payload = result.Data;

        payload.UserId = user.Id;
        var recipe = await _recipes.Create(payload);

        res.StatusCode = 201;
        await res.Negotiate(recipe);
      });

      Put("", async (req, res) =>
      {
        var user = await _auth.ExtractUserAsync(ModuleHelpers.ExtractTokenStr(req.HttpContext));
        if (user?.Id == null)
        {
          res.StatusCode = 422;
          await res.Negotiate(new ErrorResponse { Message = "Missing User from Token" });
          return;
        }

        var result =  await req.BindAndValidate<Recipe>();
        if (!result.ValidationResult.IsValid)
        {
          res.StatusCode = 400;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "Failed Validation",
              Errors = result.ValidationResult.GetFormattedErrors()
            }
          );
          return;
        }

        var recipe = result.Data;

        if (recipe.UserId != user.Id)
        {
          res.StatusCode = 403;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "You don't have access to this recipe",
            }
          );
          return;
        }
  
        var didUpdate = await _recipes.Update(recipe);
        if (!didUpdate)
        {
          res.StatusCode = 422;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "Could Update Recipe",
            }
          );
          return;
        }

        await res.Negotiate(recipe);
      });

      Delete("{id:string}", async (req, res) =>
      {
        var strId = req.RouteValues.As<string>("id");
        var recipeId = ObjectId.Parse(strId);
        var recipe = await _recipes.FindOne(recipeId);
        var user = await _auth.ExtractUserAsync(ModuleHelpers.ExtractTokenStr(req.HttpContext));
        if (recipe.UserId != user?.Id)
        {
          res.StatusCode = 403;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "You don't have access to this recipe",
            }
          );
          return;
        }

        var didDestroy = await _recipes.Destroy(recipeId);
        if (!didDestroy)
        {
          res.StatusCode = 422;
          await res.Negotiate(
            new ErrorResponse
            {
              Message = "Failed To Delete This Recipe",
            }
          );
          return;
        }

        res.StatusCode = 204;
      });
    }
  }
}
