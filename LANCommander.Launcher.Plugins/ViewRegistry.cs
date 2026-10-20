using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace LANCommander.Launcher.Plugins;

/// <inheritdoc cref="IViewRegistry" />
public sealed class ViewRegistry : IViewRegistry
{
    private readonly List<Registration> _registrations = new();

    public void Register(Type viewModelType, Func<Control> factory)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(factory);

        _registrations.Add(new Registration(viewModelType, factory));
    }

    public void Register<TViewModel>(Func<Control> factory) => Register(typeof(TViewModel), factory);

    public IDataTemplate AsDataTemplate() => new RegistryDataTemplate(_registrations);

    private readonly record struct Registration(Type ViewModelType, Func<Control> Factory);

    /// <summary>
    /// A live view over the registry's registrations. Because it holds the same list instance the
    /// registry appends to, plugin registrations made after this template is attached are still
    /// picked up.
    /// </summary>
    private sealed class RegistryDataTemplate(IReadOnlyList<Registration> registrations) : IDataTemplate
    {
        public bool Match(object? data) => data != null && FindFactory(data.GetType()) != null;

        public Control? Build(object? data) => data == null ? null : FindFactory(data.GetType())?.Invoke();

        private Func<Control>? FindFactory(Type dataType)
        {
            Type? bestType = null;
            Func<Control>? bestFactory = null;

            foreach (var registration in registrations)
            {
                if (!registration.ViewModelType.IsAssignableFrom(dataType))
                    continue;

                // Prefer the most-derived match: a candidate wins if the current best is one of its
                // base types (i.e. the candidate is more specific), or if there is no best yet.
                if (bestType == null || bestType.IsAssignableFrom(registration.ViewModelType))
                {
                    bestType = registration.ViewModelType;
                    bestFactory = registration.Factory;
                }
            }

            return bestFactory;
        }
    }
}
