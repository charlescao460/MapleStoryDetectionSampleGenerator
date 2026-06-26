using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleStory.Common;
using WzComparerR2.WzLib;

namespace MapleStory.MachineLearningSampleGenerator.Configuration
{
    internal interface IMapCatalog
    {
        IReadOnlyList<string> ListMapIds(string mapleStoryPath, Encoding encoding);
    }

    internal sealed class WzMapCatalog : IMapCatalog
    {
        public IReadOnlyList<string> ListMapIds(string mapleStoryPath, Encoding encoding)
        {
            using WzContext context = new WzContext(mapleStoryPath, encoding, false);
            context.Activate();

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<Wz_Node> mapRoots = context.WzStructure.WzNode.Nodes
                .Where(node => node.GetNodeWzFile()?.Type == Wz_Type.Map);
            foreach (Wz_Node mapRoot in mapRoots)
            {
                foreach (Wz_Node node in EnumerateDescendants(mapRoot))
                {
                    string id = GetMapId(node.Text);
                    if (id != null)
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<Wz_Node> EnumerateDescendants(Wz_Node root)
        {
            Queue<Wz_Node> queue = new Queue<Wz_Node>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Wz_Node node = queue.Dequeue();
                yield return node;

                foreach (Wz_Node child in node.Nodes)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private static string GetMapId(string nodeText)
        {
            if (string.IsNullOrWhiteSpace(nodeText) || !nodeText.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string id = nodeText.Substring(0, nodeText.Length - ".img".Length);
            return id.Length > 0 && id.All(char.IsDigit)
                ? id
                : null;
        }
    }
}
