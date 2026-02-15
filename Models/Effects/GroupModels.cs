using System;
using System.Collections.Generic;

namespace Misfitz_Games.Models.Effects;

public class DeviceGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int OwnerUserId { get; set; }

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<DeviceGroupMember> Members { get; set; } = [];
}

public class DeviceGroupMember
{
    public Guid GroupId { get; set; }
    public DeviceGroup Group { get; set; } = default!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;
}
