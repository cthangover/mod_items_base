namespace Cthangover.Mods.Items.Tests
{
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;

    /// <summary>
    /// Replaces the active inventory during validation while subsequent token checks still admit the old participant.
    /// </summary>
    public sealed class InventoryReentryConsumptionChange : IInventoryStorageParticipant
    {
        /// <summary>Models a load/new-context callback after the old owners have prepared their candidates.</summary>
        public bool IsCurrent
        {
            get
            {
                GameData.Instance.Runtime.Inventory = new Inventory();
                return true;
            }
        }

        /// <summary>The domain token alone cannot know that the active inventory was replaced.</summary>
        public bool CanApply
        {
            get
            {
                return true;
            }
        }

        /// <summary>No native fixture state needs applying; public bag assertions detect an incorrect commit.</summary>
        public void ApplySilently()
        {
        }

        /// <summary>The fixture owns no domain mutation to roll back.</summary>
        public void RollbackSilently()
        {
        }

        /// <summary>The fixture publishes no external events.</summary>
        public void Publish()
        {
        }

        /// <summary>The managed fixture token owns no disposable native resources.</summary>
        public void Dispose()
        {
        }
    }
}
