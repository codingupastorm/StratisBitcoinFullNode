using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis
{
    public class PathPrefixInsertDocumentFilter : IDocumentFilter
    {
        private readonly string _pathPrefix;

        public PathPrefixInsertDocumentFilter(string prefix)
        {
            this._pathPrefix = prefix;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var paths = swaggerDoc.Paths.Keys.ToList();
            foreach (var path in paths)
            {
                var pathToChange = swaggerDoc.Paths[path];
                swaggerDoc.Paths.Remove(path);
                swaggerDoc.Paths.Add(path.Replace("{routePrefix}", this._pathPrefix), pathToChange);

                //RoutePrefix parameter is supplied by the path modification in the step above
                //Use LINQ to remove routePrefix parameter from the path
                _ = (from o in pathToChange.Operations
                     from p in o.Value.Parameters.ToList()
                     where p.Name == "routePrefix"
                     select new { _ = o.Value.Parameters.Remove(p) }).ToList();
            }
        }
    }
}