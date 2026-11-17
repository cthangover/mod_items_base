namespace Cthangover.Core.Items
{
    using System;
    using Cthangover.Core.Settings;

    public partial class Inventory
    {
        private RetainedSaveSlot retainedInstances;

        /// <summary>
        /// A rejected identity slot stays inert until a supported load or new bag replaces it. Ordinary count
        /// operations remain usable; the current load's incomplete two-owner dispatch never admits unique work.
        /// </summary>
        public bool InstancesAvailable
        {
            get
            {
                return retainedInstances == null && SaveService.ApplyingDormancy == null;
            }
        }

        internal void RetainUnavailableInstances(RetainedSaveSlot retained)
        {
            retainedInstances = retained ?? throw new ArgumentNullException(nameof(retained));
            mutationRevision++;
        }

        internal bool WriteUnavailableInstances(ModSlot target)
        {
            if (retainedInstances == null)
            {
                return false;
            }

            if (!retainedInstances.TryCopyTo(target))
            {
                throw new InvalidOperationException("Retained inventory identity generation is no longer current");
            }

            return true;
        }
    }
}
