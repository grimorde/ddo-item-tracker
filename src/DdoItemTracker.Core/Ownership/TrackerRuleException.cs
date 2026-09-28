namespace DdoItemTracker.Core.Ownership;

/// <summary>A broken ownership rule. The message is written for the player.</summary>
public sealed class TrackerRuleException(string message) : Exception(message);
