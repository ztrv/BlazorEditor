namespace BlazorEditor.Models;

/// <summary>
/// A worked example of a valid hierarchical definition. Handy for demoing the editor
/// and for confirming that an incoming instance round-trips correctly.
/// </summary>
public static class SampleForms
{
    public static List<Field> CustomerOrder() => new()
    {
        new() { Id = "cust",  ParentId = "",          FullId = "cust",            Name = "Customer",      SortOrder = 0, FieldType = 'G' },
        new() { Id = "name",  ParentId = "cust",      FullId = "cust:name",       Name = "Full name",     SortOrder = 0, FieldType = 'T' },
        new() { Id = "email", ParentId = "cust",      FullId = "cust:email",      Name = "Email",         SortOrder = 1, FieldType = 'T' },
        new() { Id = "addr",  ParentId = "cust",      FullId = "cust:addr",       Name = "Address",       SortOrder = 2, FieldType = 'G' },
        new() { Id = "line1", ParentId = "cust:addr", FullId = "cust:addr:line1", Name = "Street",        SortOrder = 0, FieldType = 'T' },
        new() { Id = "city",  ParentId = "cust:addr", FullId = "cust:addr:city",  Name = "City",          SortOrder = 1, FieldType = 'T' },
        new() { Id = "postc", ParentId = "cust:addr", FullId = "cust:addr:postc", Name = "Postcode",      SortOrder = 2, FieldType = 'T' },

        new() { Id = "order", ParentId = "",          FullId = "order",           Name = "Order",         SortOrder = 1, FieldType = 'G' },
        new() { Id = "qty",   ParentId = "order",     FullId = "order:qty",       Name = "Quantity",      SortOrder = 0, FieldType = 'N' },
        new() { Id = "due",   ParentId = "order",     FullId = "order:due",       Name = "Delivery date", SortOrder = 1, FieldType = 'D' },
        new() { Id = "notes", ParentId = "order",     FullId = "order:notes",     Name = "Notes",         SortOrder = 2, FieldType = 'A' },
        new() { Id = "rush",  ParentId = "order",     FullId = "order:rush",      Name = "Rush delivery", SortOrder = 3, FieldType = 'B' },
    };
}
