using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Recetron.Api.Interfaces;
using Recetron.Core.Models;

namespace Recetron.Api.Services
{
  public class RecipeService : IRecipeService
  {
    private readonly IMongoCollection<Recipe> _recipes;

    public RecipeService(IDBService dbs)
    {
      _recipes = dbs.GetCollection<Recipe>("recipes");
    }

    public Task<Recipe> Create(Recipe item, CancellationToken ct = default)
    {
      return _recipes
        .InsertOneAsync(item, cancellationToken: ct)
        .ContinueWith(_ => item);
    }

    public Task<bool> Destroy(ObjectId id, CancellationToken ct = default)
    {
      return _recipes
        .DeleteOneAsync(recipe => recipe.Id == id, cancellationToken: ct)
        .ContinueWith(res => res.Result.DeletedCount == 1);
    }

    public Task<PaginationResult<Recipe>> Find(int page, int limit, Expression<Func<Recipe, bool>>? where = default, CancellationToken ct = default)
    {
      var offset = page * (limit - 1);

      if (where != null)
      {
        var whereCount = _recipes.CountDocumentsAsync(where, cancellationToken: ct);
        var whereList = _recipes.Find(where).Limit(limit).Skip(offset).ToEnumerable(ct);
        return whereCount.ContinueWith(res => new PaginationResult<Recipe> { Count = res.Result, List = whereList });
      }
      var count = _recipes.CountDocumentsAsync(FilterDefinition<Recipe>.Empty, cancellationToken: ct);
      var list = _recipes.Find(FilterDefinition<Recipe>.Empty).Limit(limit).Skip(offset).ToEnumerable(ct);
      return count.ContinueWith(res => new PaginationResult<Recipe> { Count = res.Result, List = list });
    }

    public Task<IEnumerable<Recipe>> FindByNameAsync(string recipeName, CancellationToken ct = default)
    {
      var filter = new FilterDefinitionBuilder<Recipe>().Text(recipeName, new TextSearchOptions { CaseSensitive = false });
      var result = _recipes.Find(filter).Limit(15).ToEnumerable(ct);
      return Task.FromResult(result);
    }

    public Task<Recipe> FindOne(ObjectId id, CancellationToken ct = default)
    {
      return _recipes
        .FindAsync(recipe => recipe.Id == id, cancellationToken: ct)
        .ContinueWith(res => res.Result.FirstOrDefault());
    }

    public Task<bool> Update(Recipe item, CancellationToken ct = default)
    {
      var filter = new FilterDefinitionBuilder<Recipe>().Where(recipe => recipe.Id == item.Id);
      return _recipes
        .UpdateOneAsync(filter, item.ToBsonDocument<Recipe>(), cancellationToken: ct)
        .ContinueWith(res => res.Result.ModifiedCount == 1);
    }
  }
}