namespace TechDocAI.Core.Common;

/// <summary>
/// Marker interface for services that should be registered with Scoped lifetime.
/// </summary>
public interface IScopedDependency
{
}

/// <summary>
/// Marker interface for services that should be registered with Transient lifetime.
/// </summary>
public interface ITransientDependency
{
}

/// <summary>
/// Marker interface for services that should be registered with Singleton lifetime.
/// </summary>
public interface ISingletonDependency
{
}
