# FriendLab

Avalonia dogfood recreating [find-a-friend](https://github.com/frankhaugen/find-a-friend): meet people where **three of five** interests overlap, within a geographic search radius. Friendship-oriented (not dating, no feed).

## How it works

1. **Control window** seeds a Harbor District scenario and opens multiple **user windows**.
2. Each user window is one “phone”: pick exactly five interests, set radius, drag your pin.
3. Matches require ≥3 shared interests **and** distance ≤ your radius.
4. Suggested activities come from shared tags and stay public/busy (library, trail cafe, market…).

In-memory `FriendHub` stands in for the old Cosmos/Mongo directory — edits in one window show up in the others live.

## Run

```powershell
dotnet run --project novolis-lab/labs/avalonia/FriendLab
```

## Demo cast

| User | Expected vs Alex |
|------|------------------|
| Blair / Drew / Fran | Match (geo + ≥3 interests) |
| Casey | Nearby but only 1 shared → no match |
| Eden | Same interests, far away → no match |

Try opening Alex + Blair, then deselect one shared interest on Blair and watch the match disappear.
