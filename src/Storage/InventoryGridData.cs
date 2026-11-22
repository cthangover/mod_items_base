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
                InventoryInstanceData.CheckIdentity(EquipmentCharacter);
                InventoryInstanceData.CheckIdentity(EquipmentSlot);
            }
        }
    }
}
