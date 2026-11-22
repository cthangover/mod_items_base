namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// Prepared domain side of consumption. Validation may inspect providers; swaps and rollback must contain no callbacks.
    /// </summary>
    public interface IInventoryStorageParticipant : IDisposable
    {
        /// <summary>Checks the complete prepared owner snapshot; provider access is allowed before the swap window.</summary>
        bool IsCurrent { get; }

        /// <summary>Checks only owned references and revisions, without callbacks.</summary>
        bool CanApply { get; }

        /// <summary>Installs preallocated state without notifying external observers.</summary>
        void ApplySilently();
        /// <summary>Restores the exact prior state before any participant publishes.</summary>
        void RollbackSilently();
        /// <summary>Notifies observers only after all participants have committed.</summary>
        void Publish();
    }
}
