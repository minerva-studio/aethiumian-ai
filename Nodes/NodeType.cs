using System;

namespace Amlos.AI
{

    [Obsolete]
    public enum NodeType
    {
        none,

        decision,
        loop,
        sequence,
        condition,
        probability,
        always,
        inverter,

        call,
        determine,
        action,
    }

    public static class NodeTypeExtension
    {
        public static string GetTip(string name)
        {
            switch (name)
            {
                case "none":
                    return "No node";
                case nameof(Decision):
                    return "Create a decision making process, execute a list of nodes in order until one child node return true";
                case nameof(ForEach):
                    return "A For-Each loop";
                case nameof(Loop):
                    return "A loop, can be either repeat by given number of times or matching certain condition";
                case nameof(Sequence):
                    return "A sequence, always execute a list of nodes in order";
                case nameof(Condition):
                    return "An if-else structure";
                case nameof(Probability):
                    return "Execute one of child by chance once";
                case nameof(Always):
                    return "Always return a value regardless the return value of its child";
                case nameof(Constant):
                    return "Always return a value regardless the return value of its child";
                case nameof(Inverter):
                    return "An inverter of the return value of its child node";
                case nameof(Call):
                    return "A type of nodes that calls certain methods";
                case nameof(DetermineBase):
                case nameof(Determine):
                    return "A type of nodes that return true/false by determine conditions given";
                case nameof(Action):
                    return "A type of nodes that perform certain actions";
                case nameof(Pause):
                    return "Pause the behaviour tree";
                default:
                    break;
            }
            return "!!!Node type not found!!!";
        }
    }

}