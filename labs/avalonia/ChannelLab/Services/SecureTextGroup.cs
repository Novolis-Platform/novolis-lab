using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Novolis.Chat.Abstractions;
using Novolis.Chat.Directory;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelLab.Services;

internal sealed record SecureTextGroup(
    SecureTextGroupId GroupId,
    string Name,
    string InitiatorNick,
    IReadOnlyList<SecureTextGroupMember> Members,
    IReadOnlyList<Guid> ApprovedDeviceIds,
    bool IsLocallyApproved)
{
    public bool IsActive =>
        IsLocallyApproved
        && Members.Count == ApprovedDeviceIds.Count
        && Members.All(member => ApprovedDeviceIds.Contains(member.DeviceId.Value));

    public override string ToString()
    {
        var roster = string.Join(", ", Members.Select(member => member.Nick));
        return IsActive ? $"{Name} ({roster}) · active" : $"{Name} ({roster}) · approval pending";
    }
}
