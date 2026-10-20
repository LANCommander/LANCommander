using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Helpers
{
    /// <summary>Which redistributables to install and uninstall so an install matches its game version.</summary>
    public class RedistributableSyncPlan
    {
        public List<Guid> Install { get; } = new();
        public List<Guid> Uninstall { get; } = new();
    }

    /// <summary>
    /// Works out how to bring an install's redistributables in line with the version it's on: the ones the
    /// version lists but aren't installed get installed, and installed ones only a previous version listed
    /// get uninstalled. Redistributables were installed per game install, so removing one doesn't touch
    /// other games.
    /// </summary>
    public static class RedistributableSyncPlanner
    {
        /// <param name="previous">Redistributables the install's previous version listed.</param>
        /// <param name="target">Redistributables the version being moved to (and any installed addons) list.</param>
        /// <param name="isInstalled">Whether a redistributable is installed in this install directory.</param>
        public static RedistributableSyncPlan Plan(IEnumerable<Guid> previous, IEnumerable<Guid> target, Func<Guid, bool> isInstalled)
        {
            var plan = new RedistributableSyncPlan();
            var targetSet = target.ToHashSet();

            plan.Install.AddRange(targetSet.Where(id => !isInstalled(id)));
            plan.Uninstall.AddRange(previous.Distinct().Where(id => !targetSet.Contains(id) && isInstalled(id)));

            return plan;
        }
    }
}
