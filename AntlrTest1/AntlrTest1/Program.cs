using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using Antlr.Runtime;
using Antlr.Runtime.Tree;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;

namespace AntlrTest1
{
    public class WorkOrderIndexStore
    {
        public virtual MongoDatabase GetDB()
        {
            var client = new MongoClient("mongodb://localhost");
            var server = client.GetServer();
            var db = server.GetDatabase("PulseAD");
            db.SetProfilingLevel(ProfilingLevel.All);
            return db;
        }

        public void Put(WorkOrderIndex workOrderIndex)
        {
            var collection = WorkOrderSearchIndex();
            collection.Save(workOrderIndex);
        }

        private MongoCollection<BsonDocument> WorkOrderSearchIndex()
        {
            var db = GetDB();
            var collection = db.GetCollection("WorkOrderSearchListIndex");
            return collection;
        }

        public List<Guid> Find(string q)
        {
            var results = new List<Guid>();
            var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(q);
             IMongoQuery query = new QueryDocument(doc);
            var cursor = WorkOrderSearchIndex().Find(query);

            //var query = Query.Or(Query.EQ("Name:", "WorkOrder4"), Query.EQ("Name:", "WorkOrder20344"));
            //var a = WorkOrderSearchIndex().FindAs<WorkOrderIndex>(query);
            
            
            foreach (var record in cursor.Take(10))
            {
                var item = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<WorkOrderIndex>(record);
                results.Add(item.Id);
            }
            return results;
        }

        public void Remove(Guid id)
        {
            var query = Query.And(Query.EQ("Id", id));
            WorkOrderSearchIndex().Remove(query);
        }
    }

    public class WorkOrderIndex
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Duration { get; set; }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            var rand = new Random();

            var a = new WorkOrderIndexStore();
//            for (var i = 0; i < 1000000; i++)
//            {
//                if (i%1000 == 0)
//                {
//                    Console.WriteLine(i);
//                }
//                a.Put(new WorkOrderIndex {Id = Guid.NewGuid(), Name = "TEST", Duration = Math.Round(rand.NextDouble() * 1000,2)});
//            }
            var searchString = "Duration: > 990";
            var p = new CombinedParser(new CommonTokenStream(new CombinedLexer(new ANTLRStringStream(searchString))));
            var tree = p.searchString().Tree;
            var nodes = MarchTree(tree);
            var visitor = new MongoDbNodeVisitor();
            visitor.Visit((dynamic)nodes, null);
            var mongoString = "{" + nodes.DBText + "}";
            var stop = new Stopwatch();
            stop.Start();
            var list = a.Find(mongoString);
            Console.WriteLine(stop.ElapsedMilliseconds);
            Console.WriteLine("Records:" + list.Count);
        }

        public static INode MarchTree(CommonTree tree)
        {
            var nodeVisitor = new NodeVisitor();
            var currentNode = GenerateNode(tree.Type);
            var children = new List<INode>();
            if (tree.Children != null)
                children.AddRange(tree.Children.Select(child => MarchTree((CommonTree) child)));

            currentNode.Accept(nodeVisitor, new RequiredData
                {
                    Children = children,
                    Value = tree.Text
                });
            return currentNode;
        }

        private static INode GenerateNode(int type)
        {
            switch (type)
            {
                case CombinedLexer.AND: return new LogicalOperation { Operator = new And() };
                case CombinedLexer.OR: return new LogicalOperation { Operator = new Or() };
                case CombinedLexer.FIELDNAME: return new Field();
                case CombinedLexer.QUOTEDVALUE: return new Value();
                case CombinedLexer.VALUE: return new Value();
                case CombinedLexer.OPERATOR: return new Comparison();
                default: return new Root();
            }
        }
    }

    public interface INodeVisitor
    {
        void Visit(LogicalOperation node, RequiredData data);
        void Visit(Field node, RequiredData data);
        void Visit(Value node, RequiredData data);
        void Visit(Comparison node, RequiredData data);
        void Visit(Root node, RequiredData data);
    }

    public class RequiredData
    {
        public string Value;
        public List<INode> Children;
    }

    public class MongoDbNodeVisitor : INodeVisitor
    {
        public void Visit(LogicalOperation node, RequiredData data)
        {
            Visit((dynamic)node.Lhs, null);
            Visit((dynamic)node.Rhs, null);
            var oper = node.Operator is And ? "and" : "or";
            var partial = string.Format("${0}:[{{{1}}},{{{2}}}]", oper, node.Lhs.DBText, node.Rhs.DBText);
            node.DBText += partial;
        }

        public void Visit(Field node, RequiredData data)
        {
            Visit(node.Relation, null);
            var partial = node.FieldName + string.Format(node.Relation.DBText, node.FieldValue.FieldValue.Trim('"'));
            node.DBText += partial;
        }

        public void Visit(Value node, RequiredData data)
        { }

        public void Visit(Comparison node, RequiredData data)
        {
            string result = "";

            switch (node.Operator)
            {
                case "!=": result = "$ne"; break;
                case "<":  result = "$lt"; break;
                case "<=": result = "$lte"; break;
                case ">":  result = "$gt"; break;
                case ">=": result = "$gte"; break;
                //case "=":  result = ""; break;
            }
            if (string.IsNullOrEmpty(result))
            {
                node.DBText += "\"{0}\"";
            }
            else
            {
                node.DBText += "{{ " + result + ": {0}}}";
            }
            
        }

        public void Visit(Root node, RequiredData data)
        {}
    }

    class NodeVisitor : INodeVisitor
    {
        public void Visit(LogicalOperation node, RequiredData data)
        {
            node.Lhs = data.Children[0];
            node.Rhs = data.Children[1];
        }

        public void Visit(Field node, RequiredData data)
        {
            node.FieldName = data.Value;
            node.FieldValue = (Value)data.Children.FirstOrDefault(x => x is Value);
            node.Relation = (Comparison) data.Children.FirstOrDefault(x => x is Comparison) ?? new Comparison {Operator = "CONTAINS"};
        }

        public void Visit(Value node, RequiredData data)
        {
            node.FieldValue = data.Value;
        }

        public void Visit(Comparison node, RequiredData data)
        {
            node.Operator = data.Value;
        }

        public void Visit(Root node, RequiredData data)
        {
            node.Child = data.Children.FirstOrDefault();
        }
    }

    public interface INode
    {
        string DBText { get; set; }
        void Accept(INodeVisitor visitor, RequiredData data);
    }

    public class LogicalOperation : INode
    {
        public Operator Operator;
        public INode Lhs;
        public INode Rhs;

        public string DBText { get; set; }

        public void Accept(INodeVisitor visitor, RequiredData data)
        {
            visitor.Visit(this, data);
        }
    }

    public class Operator
    {}

    public class And : Operator
    {
        public string Name = "And";
    }
    
    public class Or : Operator
    {
        public string Name = "Or";
    }

    public class Comparison : INode
    {
        public string Operator;
        public string DBText { get; set; }

        public void Accept(INodeVisitor visitor, RequiredData data)
        {
            visitor.Visit(this, data);
        }
    }

    public class Field : INode
    {
        public string FieldName;
        public Type DataType;
        public Value FieldValue;
        public Comparison Relation;
        public string DBText { get; set; }

        public void Accept(INodeVisitor visitor, RequiredData data)
        {
            visitor.Visit(this, data);
        }
    }

    public class Value : INode
    {
        public string FieldValue;
        public string DBText { get; set; }

        public void Accept(INodeVisitor visitor, RequiredData data)
        {
            visitor.Visit(this, data);
        }
    }

    public class Root : INode
    {
        public INode Child;
        public string DBText { get; set; }

        public void Accept(INodeVisitor visitor, RequiredData data)
        {
            visitor.Visit(this, data);
        }
    }
//
//    public class UnknownNode : INode
//    {
//        public string DBText { get; set; }
//        public void Accept(INodeVisitor visitor, RequiredData data)
//        {
//            throw new NotImplementedException();
//        }
//    }
}
