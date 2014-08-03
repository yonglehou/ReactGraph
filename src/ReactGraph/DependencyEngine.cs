using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ReactGraph.Internals.Api;
using ReactGraph.Internals.Construction;
using ReactGraph.Internals.Graph;
using ReactGraph.Internals.NodeInfo;

namespace ReactGraph
{
    public class DependencyEngine
    {
        private readonly DirectedGraph<INodeInfo> graph;
        private readonly ExpressionParser expressionParser;
        private readonly NodeRepository nodeRepository;
        private bool isExecuting;

        public DependencyEngine()
        {
            graph = new DirectedGraph<INodeInfo>();
            nodeRepository = new NodeRepository(this);
            expressionParser = new ExpressionParser();
        }

        public bool ValueHasChanged(object instance, string key)
        {
            if (!nodeRepository.Contains(instance, key) || isExecuting) return false;

            var node = nodeRepository.Get(instance, key);

            try
            {
                isExecuting = true;
                var orderToReeval = new Queue<Vertex<INodeInfo>>(graph.TopologicalSort(node));
                var firstVertex = orderToReeval.Dequeue();
                node.ValueChanged();
                NotificationStratgegyValueUpdate(firstVertex);
                while (orderToReeval.Count > 0)
                {
                    var vertex = orderToReeval.Dequeue();
                    var results = vertex.Data.Reevaluate();
                    if (results == ReevalResult.Error)
                    {
                        var nodesRelatedToError = graph.TopologicalSort(vertex.Data).ToDictionary(k => k.Data);
                        var newListToProcess = orderToReeval
                            .Where(remaining => !nodesRelatedToError.ContainsKey(remaining.Data))
                            .ToArray();
                        orderToReeval = new Queue<Vertex<INodeInfo>>(newListToProcess);
                    }
                    NotificationStratgegyValueUpdate(vertex);
                }
            }
            finally
            {
                isExecuting = false;
            }

            return true;
        }

        static void NotificationStratgegyValueUpdate(Vertex<INodeInfo> firstVertex)
        {
            foreach (var successor in firstVertex.Successors)
            {
                firstVertex.Data.UpdateSubscriptions(successor.Source.Data.GetValue());
            }
        }

        public override string ToString()
        {
            return graph.ToDotLanguage("DependencyGraph");
        }

        public IExpressionDefinition<TProp> Expr<TProp>(Expression<Func<TProp>> sourceFunction)
        {
            var formulaNode = expressionParser.GetFormulaInfo(sourceFunction);
            return new ExpressionDefinition<TProp>(formulaNode, expressionParser, graph, nodeRepository);
        }
    }
}