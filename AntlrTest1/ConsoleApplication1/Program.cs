using System;
using System.Collections.Generic;

namespace ConsoleApplication1
{
    public class Program
    {
        public static void Main()
        {
            var nodes = new INode[] { new Operator(), new Field(), new Value(), new Value(), new Value(), new Operator() };

            var nodeWriter = new NodeWriter();
            foreach (var node in nodes)
            {
                node.Accept(nodeWriter);
            }
            Console.WriteLine("Operators.Count: {0}", nodeWriter.Operators.Count);
            Console.WriteLine("Fields.Count: {0}", nodeWriter.Fields.Count);
            Console.WriteLine("Values.Count: {0}", nodeWriter.Values.Count);
        }
    }

    interface INodeVisitor
    {
        void Visit(Operator node);
        void Visit(Field node);
        void Visit(Value node);
    }

    interface INode { void Accept(INodeVisitor visitor); }
    class Operator : INode { public void Accept(INodeVisitor visitor) { visitor.Visit(this); } }
    class Field : INode { public void Accept(INodeVisitor visitor) { visitor.Visit(this); } }
    class Value : INode { public void Accept(INodeVisitor visitor) { visitor.Visit(this); } }

    class NodeWriter : INodeVisitor
    {
        public List<Operator> Operators { get; private set; }
        public List<Field> Fields { get; private set; }
        public List<Value> Values { get; private set; }

        public NodeWriter()
        {
            Operators = new List<Operator>();
            Fields = new List<Field>();
            Values = new List<Value>();
        }

        public void Visit(Operator node) { Operators.Add(node); }
        public void Visit(Field node) { Fields.Add(node); }
        public void Visit(Value node) { Values.Add(node); }
    }
}
