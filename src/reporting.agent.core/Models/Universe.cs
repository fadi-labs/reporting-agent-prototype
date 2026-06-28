namespace reporting.agent.core.Models;

public enum Universe
{
    CustomerOrder,
    ShipperBooking,
    CarrierBooking,
    CargoStuffing,
    ShippingInstruction,
    EventsAndMilestones,
    Destination,
    CustomerMessagingService,
}

public static class UniverseMap
{
    public static readonly IReadOnlyDictionary<Universe, string> DisplayName =
        new Dictionary<Universe, string>
        {
            [Universe.CustomerOrder] = "Customer Order",
            [Universe.ShipperBooking] = "Shipper Booking",
            [Universe.CarrierBooking] = "Carrier Booking",
            [Universe.CargoStuffing] = "Cargo Stuffing",
            [Universe.ShippingInstruction] = "Shipping Instruction",
            [Universe.EventsAndMilestones] = "Events And Milestones",
            [Universe.Destination] = "Destination",
            [Universe.CustomerMessagingService] = "Customer Messaging Service",
        };

    public static readonly IReadOnlyDictionary<Universe, string> FileStem =
        new Dictionary<Universe, string>
        {
            [Universe.CustomerOrder] = "customer_order",
            [Universe.ShipperBooking] = "shipper_booking",
            [Universe.CarrierBooking] = "carrier_booking",
            [Universe.CargoStuffing] = "cargo_stuffing",
            [Universe.ShippingInstruction] = "shipping_instruction",
            [Universe.EventsAndMilestones] = "events_and_milestones",
            [Universe.Destination] = "destination",
            [Universe.CustomerMessagingService] = "customer_messaging_service",
        };

    public static readonly IReadOnlyDictionary<string, Universe> ByFileStem =
        FileStem.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly IReadOnlyDictionary<string, Universe> ByDisplayName =
        DisplayName.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string value, out Universe universe)
    {
        if (ByDisplayName.TryGetValue(value, out universe))
        {
            return true;
        }
        if (ByFileStem.TryGetValue(value.Trim().ToLowerInvariant(), out universe))
        {
            return true;
        }
        universe = default;
        return false;
    }
}

