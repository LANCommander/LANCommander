namespace LANCommander.UI.Providers;

public static class QuipProvider
{
    private static readonly string[] CrashQuips =
    {
        "You Died.",
        "Snake? SNAAAAAAKE!",
        "WASTED",
        "Major fracture detected",
        "The past is a gaping hole. You try to run from it, but the more you run, the deeper, the darker, the bigger it gets.",
        "Your town center has been destroyed",
        "Your forces are under attack!",
        "You have lost the lead",
        "Terrorists Win",
        "War... War never changes.",
        "You have died of dysentery",
        "You have failed to restore the books. The Ages are lost.",
        "Player was splattered by a demon",
        "Sure, blame it on your ISP",
        "Baba is no more",
        "Guests are complaining they are lost",
        "The darkness has overcome you",
        "Subject: Gordon Freeman. Status: Terminated",
        "Mission failed: You were spotted.",
        "Critical damage! Eject, eject!",
        "Your minions are unhappy. They are leaving.",
        "The Empire has triumphed",
        "Your quest has ended in failure",
        "You have been eaten by a grue",
        "You no mess with Lo Wang!",
        "Sam was killed. Serious carnage ensues.",
        "Damn, those alien bastards are gonna pay for shooting up my ride",
    };

    private static readonly string[] NotFoundQuips =
    {
        "You must construct additional pylons",
        "I can't build that there!",
        "You cannot go that way.",
        "This isn't the place for that",
        "I can't use that",
        "That doesn't work.",
        "You must gather your party before venturing forth.",
        "Out of ammo.",
        "Target lost",
        "The cake is a lie",
        "Roads required",
        "Not enough minerals",
        "This door is inaccessible.",
        "You cannot carry any more.",
        "Warning: Entering ecological dead zone.",
        "Map not found.",
        "Not enough mana."
    };

    public static string GetCrashQuip()
    {
        var randIndex = new Random().Next(0, CrashQuips.Length - 1);

        return CrashQuips[randIndex];
    }

    public static string GetNotFoundQuip()
    {
        var randIndex = new Random().Next(0, NotFoundQuips.Length - 1);
        
        return NotFoundQuips[randIndex];
    }
}