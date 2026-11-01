namespace ApexPost.Web.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ProtectedIntegrationEndpointAttribute : Attribute
{
}
