namespace Services;

public sealed class IdentityService
{
    public string Resolve(string name) => name;
}

internal sealed class HiddenIdentity
{
}
