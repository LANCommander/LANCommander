using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Plugins;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Verifies the <see cref="ViewRegistry"/> data template resolves view models to controls, and in
/// particular preserves the launcher's most-derived-first rule (a DepotGameDetail-style subclass
/// must resolve to its own view before the base GameDetail mapping is consulted), regardless of the
/// order registrations were added.
/// </summary>
public class ViewRegistryTests
{
    private class BaseViewModel { }

    private class DerivedViewModel : BaseViewModel { }

    private class UnrelatedViewModel { }

    private class BaseView : ContentControl { }

    private class DerivedView : ContentControl { }

    [AvaloniaFact]
    public void AsDataTemplate_ResolvesRegisteredViewModel_ToControl()
    {
        var registry = new ViewRegistry();
        registry.Register<BaseViewModel>(() => new BaseView());

        var template = registry.AsDataTemplate();
        var data = new BaseViewModel();

        Assert.True(template.Match(data));
        Assert.IsType<BaseView>(template.Build(data));
    }

    [AvaloniaFact]
    public void AsDataTemplate_PrefersMostDerivedType_WhenBaseRegisteredFirst()
    {
        var registry = new ViewRegistry();
        registry.Register<BaseViewModel>(() => new BaseView());
        registry.Register<DerivedViewModel>(() => new DerivedView());

        var template = registry.AsDataTemplate();

        Assert.IsType<DerivedView>(template.Build(new DerivedViewModel()));
        Assert.IsType<BaseView>(template.Build(new BaseViewModel()));
    }

    [AvaloniaFact]
    public void AsDataTemplate_PrefersMostDerivedType_WhenDerivedRegisteredFirst()
    {
        var registry = new ViewRegistry();
        registry.Register<DerivedViewModel>(() => new DerivedView());
        registry.Register<BaseViewModel>(() => new BaseView());

        var template = registry.AsDataTemplate();

        // Registration order must not change the resolution: the derived VM still gets its own view.
        Assert.IsType<DerivedView>(template.Build(new DerivedViewModel()));
        Assert.IsType<BaseView>(template.Build(new BaseViewModel()));
    }

    [AvaloniaFact]
    public void AsDataTemplate_DoesNotMatch_UnregisteredType()
    {
        var registry = new ViewRegistry();
        registry.Register<BaseViewModel>(() => new BaseView());

        var template = registry.AsDataTemplate();

        Assert.False(template.Match(new UnrelatedViewModel()));
        Assert.Null(template.Build(new UnrelatedViewModel()));
    }

    [AvaloniaFact]
    public void AsDataTemplate_ReadsRegistrationsLive_ForRegistrationsAddedAfterTemplateCreated()
    {
        var registry = new ViewRegistry();
        var template = registry.AsDataTemplate();

        // Plugins register navigable views after the shell has already attached the template.
        registry.Register<BaseViewModel>(() => new BaseView());

        Assert.True(template.Match(new BaseViewModel()));
        Assert.IsType<BaseView>(template.Build(new BaseViewModel()));
    }

    [AvaloniaFact]
    public void Match_ReturnsFalse_ForNullData()
    {
        var registry = new ViewRegistry();
        registry.Register<BaseViewModel>(() => new BaseView());

        Assert.False(registry.AsDataTemplate().Match(null));
    }
}
