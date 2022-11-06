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
                case "Decision":
                    return "Create a decision making process, execute a list of nodes in order until one child node return true";
                case "Loop":
                    return "A loop, can be either repeat by given number of times or matching certain condition";
                case "Sequence":
                    return "A sequence, always execute a list of nodes in order";
                case "Condition":
                    return "An if-else structure";
                case "Probability":
                    return "Execute one of child by chance once";
                case "Always":
                    return "Always return a value regardless the return value of its child";
                case "Inverter":
                    return "An inverter of the return value of its child node";
                case "Call":
                    return "A type of nodes that calls certain methods";
                case "Determine":
                    return "A type of nodes that return true/false by determine conditions given";
                case "Action":
                    return "A type of nodes that perform certain actions";
                case "Pause":
                    return "Pause the behaviour tree";
                default:
                    break;
            }
            return "!!!Node type not found!!!";
        }
    }

}