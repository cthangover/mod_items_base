namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// One protected occurrence displayed in the normal bag. Its count cannot be split or silently stacked;
    /// only the inventory's prepared identity API can transfer the descriptor and its derived row.
    /// </summary>
    public sealed class InventoryInstanceContainer : IItemContainer
    {
        private readonly IItem item;

        /// <summary>Stable occurrence key used for selection and identity-aware actions, rather than Item.ID.</summary>
        public string InstanceId { get; }

        /// <summary>The borrowed shared blueprint cannot be reassigned through the legacy mutable container API.</summary>
        public IItem Item
        {
            get
            {
                return item;
            }
            set
            {
                if (!ReferenceEquals(item, value))
                {
                    throw new InvalidOperationException("Unique inventory rows cannot change their blueprint");
                }
            }
        }

        /// <summary>A unique occurrence is exactly one; count-only mutation cannot consume or merge its identity.</summary>
        public int Count
        {
            get
            {
                return 1;
            }
            set
            {
                if (value != 1)
                {
                    throw new InvalidOperationException("Unique inventory rows cannot change their count");
                }
            }
        }

        internal InventoryInstanceContainer(string id, IItem item)
        {
            InstanceId = id;
            this.item = item ?? throw new ArgumentNullException(nameof(item));
        }
    }
}
