using System.Linq;

using JetBrains.Metadata.Reader.API;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Impl.Reflection2;
using JetBrains.ReSharper.UnitTestFramework;
using JetBrains.ReSharper.UnitTestFramework.Elements;

using Machine.Specifications.ReSharperRunner.Presentation;
using Machine.Specifications.ReSharperRunner.Shims;

using ICache = Machine.Specifications.ReSharperRunner.Shims.ICache;

namespace Machine.Specifications.ReSharperRunner.Factories
{
  [SolutionComponent]
  public class BehaviorFactory
  {
    readonly ElementCache _cache;
    readonly IUnitTestElementManager _elementManager;
    readonly ICache _cacheManager;
    readonly IProject _project;
    readonly ProjectModelElementEnvoy _projectEnvoy;
    readonly MSpecUnitTestProvider _provider;
    readonly IPsi _psiModuleManager;
    readonly ReflectionTypeNameCache _reflectionTypeNameCache = new ReflectionTypeNameCache();

    public BehaviorFactory(MSpecUnitTestProvider provider,
                           IPsi psiModuleManager,
                           ICache cacheManager,
                           IProject project,
                           ProjectModelElementEnvoy projectEnvoy,
                           ElementCache cache,
                           IUnitTestElementManager elementManager)
    {
      _psiModuleManager = psiModuleManager;
      _cacheManager = cacheManager;
      _provider = provider;
      _cache = cache;
      _elementManager = elementManager;
      _project = project;
      _projectEnvoy = projectEnvoy;
    }

    public BehaviorElement CreateBehavior(IDeclaredElement field)
    {
      var clazz = ((ITypeMember) field).GetContainingType() as IClass;
      if (clazz == null)
      {
        return null;
      }

      ContextElement context;
      _cache.Contexts.TryGetValue(clazz, out context);
      if (context == null)
      {
        return null;
      }

      var fieldType = new NormalizedTypeName(field as ITypeOwner);

      var behavior = GetOrCreateBehavior(context,
                                         clazz.GetClrName(),
                                         field.ShortName,
                                         field.IsIgnored(),
                                         fieldType);

      foreach (var child in behavior.Children)
      {
        child.State = UnitTestElementState.Pending;
      }

      _cache.Behaviors.Add(field, behavior);
      return behavior;
    }

    public BehaviorElement CreateBehavior(ContextElement context, IMetadataField behavior)
    {
      var typeContainingBehaviorSpecifications = behavior.GetFirstGenericArgument();

      var metadataTypeName = behavior.FirstGenericArgumentClass().FullyQualifiedName();
      var fieldType = new NormalizedTypeName(new ClrTypeName(metadataTypeName));

      var behaviorElement = GetOrCreateBehavior(context,
                                                _reflectionTypeNameCache.GetClrName(behavior.DeclaringType),
                                                behavior.Name,
                                                behavior.IsIgnored() || typeContainingBehaviorSpecifications.IsIgnored(),
                                                fieldType);

      return behaviorElement;
    }

    public BehaviorElement GetOrCreateBehavior(ContextElement context,
                                               IClrTypeName declaringTypeName,
                                               string fieldName,
                                               bool isIgnored,
                                               string fieldType)
    {
      var id = BehaviorElement.CreateId(context, fieldType, fieldName);
      var behavior = _elementManager.GetElementById(_project, id) as BehaviorElement;
      if (behavior != null)
      {
        behavior.Parent = context;
        behavior.State = UnitTestElementState.Valid;
        return behavior;
      }

      return new BehaviorElement(_provider,
                                 _psiModuleManager,
                                 _cacheManager,
                                 context,
                                 _projectEnvoy,
                                 declaringTypeName,
                                 fieldName,
                                 isIgnored,
                                 fieldType);
    }

    public void UpdateChildState(IDeclaredElement field)
    {
      BehaviorElement behavior;
      if (!_cache.Behaviors.TryGetValue(field, out behavior))
      {
        return;
      }

      foreach (var element in behavior
        .Children.Where(x => x.State == UnitTestElementState.Pending)
        .Flatten(x => x.Children))
      {
        element.State = UnitTestElementState.Invalid;
      }
    }
  }
}