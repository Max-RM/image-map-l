using fNbt;
using TryashtarUtils.Nbt;

namespace ImageMap4;

// How imported items are placed into an inventory: one item per slot, or packed into
// chests (recursively on Bedrock) so that many items can share a single slot.
public enum InventoryMode
{
    Items = 0,
    Chests = 1
}

public interface IInventory
{
    string Name { get; }
    // Number of usable empty slots, used to warn before an import would not fit.
    int FreeSlots { get; }
    void AddItems(IEnumerable<NbtCompound> items);
}

// I would prefer if we just used null for this, but we need something to show up for "Name" in the combobox
public class NoInventory : IInventory
{
    public string Name => "None";
    public int FreeSlots => int.MaxValue;
    public void AddItems(IEnumerable<NbtCompound> items) { }
}

public class JavaInventory : IInventory
{
    public string Name { get; private set; }
    public readonly NbtFile File;
    public readonly NbtList Inventory;
    public JavaInventory(string name, NbtFile file, NbtList inventory)
    {
        Name = name;
        File = file;
        Inventory = inventory;
    }

    public int FreeSlots
    {
        get
        {
            var occupied = Inventory.Cast<NbtCompound>()
                .Select(x => x.Get<NbtByte>("Slot"))
                .Where(x => x != null)
                .Select(x => x!.Value)
                .Distinct()
                .Count();
            return Math.Max(0, 36 - occupied);
        }
    }

    public void AddItems(IEnumerable<NbtCompound> items)
    {
        var occupied_slots = Inventory.Cast<NbtCompound>().Select(x => x.Get<NbtByte>("Slot").Value).ToHashSet();
        foreach (var item in items)
        {
            for (byte i = 0; i < 36; i++)
            {
                if (!occupied_slots.Contains(i))
                {
                    item.Add(new NbtByte("Slot", i));
                    Inventory.Add(item);
                    occupied_slots.Add(i);
                    break;
                }
            }
        }
        File.SaveToFile(File.FileName, File.FileCompression);
    }
}

public class BedrockInventory : IInventory
{
    public string Name { get; }
    public readonly BedrockWorld World;
    public readonly string Key;
    public BedrockInventory(string name, BedrockWorld world, string key)
    {
        // Name could also be a UUID, but it seems impossible to get a username from this
        Name = name;
        World = world;
        Key = key;
    }

    public int FreeSlots
    {
        get
        {
            var db = World.OpenDB();
            var bytes = db.Get(Key);
            if (bytes == null)
                return 0;
            var file = new NbtFile() { BigEndian = false };
            file.LoadFromBuffer(bytes, 0, bytes.Length, NbtCompression.None);
            var inventory = file.GetRootTag<NbtCompound>()?.Get<NbtList>("Inventory");
            if (inventory == null)
                return 0;
            return inventory.Cast<NbtCompound>().Count(x => x.Get<NbtByte>("Count")?.Value == 0);
        }
    }

    public void AddItems(IEnumerable<NbtCompound> items)
    {
        var db = World.OpenDB();
        var bytes = db.Get(Key);
        var file = new NbtFile() { BigEndian = false };
        file.LoadFromBuffer(bytes, 0, bytes.Length, NbtCompression.None);
        var inventory = file.GetRootTag<NbtCompound>().Get<NbtList>("Inventory");
        // remember bedrock saves empty slots
        foreach (var item in items)
        {
            foreach (NbtCompound slot in inventory.ToList())
            {
                if (slot.Get<NbtByte>("Count").Value == 0)
                {
                    inventory.Remove(slot);
                    item.Add((NbtByte)slot.Get<NbtByte>("Slot").Clone());
                    inventory.Add(item);
                    break;
                }
            }
        }
        bytes = file.SaveToBuffer(NbtCompression.None);
        db.Put(Key, bytes);
    }
}
