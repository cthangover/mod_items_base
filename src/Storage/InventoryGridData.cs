namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// One rectangular storage compartment. Zero dimensions preserve the legacy unbounded bag; equipment uses its own compartments.
    /// </summary>
    public sealed class InventoryGridData
    {
        /// <summary>Stable compartment address used by placements and save data.</summary>
        public string Id { get; set; } = "player";
        /// <summary>grid uses item footprints; slots uses one cell per group; list has no geometric capacity.</summary>
        public string Layout { get; set; } = "grid";
        /// <summary>Columns in cells; zero selects unbounded legacy storage.</summary>
        public int Width { get; set; }
        /// <summary>Rows in cells; zero is valid only with zero width.</summary>
        public int Height { get; set; }
        /// <summary>Optional character whose equipment owner controls this compartment.</summary>
        public string EquipmentCharacter { get; set; }
        /// <summary>Equipment slot paired with EquipmentCharacter, independent of a blueprint ID.</summary>
        public string EquipmentSlot { get; set; }

        /// <summary>A compartment has either two bounded positive dimensions or two zero dimensions.</summary>
        public void Validate()
        {
            InventoryInstanceData.CheckIdentity(Id);
            if (Layout != "grid" && Layout != "slots" && Layout != "list"
                || Layout == "list" && (Width != 0 || Height != 0))
            {
                throw new ArgumentException("Invalid inventory layout");
            }

            if (Width < 0 || Height < 0 || Width > 256 || Height > 256 || (Width == 0) != (Height == 0))
            {
                throw new ArgumentException("Invalid inventory container dimensions");
            }

            if ((EquipmentCharacter == null) != (EquipmentSlot == null))
            {
                throw new ArgumentException("Incomplete inventory equipment compartment");
            }

            if (EquipmentCharacter != null)
            {
                if (Layout != "grid")
                {
                    throw new ArgumentException("Equipment compartments cannot change their layout");
                }

                InventoryInstanceData.CheckIdentity(EquipmentCharacter);
                InventoryInstanceData.CheckIdentity(EquipmentSlot);
            }
        }

        /// <summary>Projects the preserved item footprint into this compartment's capacity rules.</summary>
        public int ItemWidth(InventoryStackData item)
        {
            return Layout == "grid" ? item.OccupiedWidth : 1;
        }

        /// <summary>Slots and lists never charge a group for its physical height.</summary>
        public int ItemHeight(InventoryStackData item)
        {
            return Layout == "grid" ? item.OccupiedHeight : 1;
        }
    }
}
