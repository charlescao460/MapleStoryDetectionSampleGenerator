using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WzComparerR2.WzLib;
using MapleStory.Common.Exceptions;

namespace MapleStory.Common
{
    public static class WzTreeSearcher
    {
        /// <summary>
        /// Generic searching based on BFS traversal. This is tree-search (only search for children).
        /// </summary>
        /// <param name="root">Root of target tree. The origin of search.</param>
        /// <param name="determinator">Return true if the input node meet search criteria.</param>
        /// <returns>Null if not found, otherwise the target node.</returns>
        public static Wz_Node GenericBfsSearcher(Wz_Node root, Func<Wz_Node, bool> determinator)
        {
            Queue<Wz_Node> queue = new Queue<Wz_Node>();
            queue.Enqueue(root);
            while (queue.Count != 0)
            {
                Wz_Node currNode = queue.Dequeue();
                if (determinator(currNode))
                {
                    return currNode;
                }
                foreach (var child in currNode.Nodes)
                {
                    queue.Enqueue(child);
                }
            }
            return null;
        }

        /// <summary>
        /// Search for specified map Img starting from Base.wz root.
        /// </summary>
        /// <param name="root">The root of Base.wz.</param>
        /// <param name="imgText">The node text to search. E.g. "450007010.img" </param>
        /// <param name="stringWzFile">Output Wz_File for <see cref="WzComparerR2.Common.StringLinker"/></param>
        /// <returns>The Wz img containing desired map.</returns>
        /// <exception cref="WzImgNotFoundException">If not found.</exception>
        public static Wz_Image SearchForMap(Wz_Node root, string imgText, out Wz_File stringWzFile)
        {
            if (!imgText.Contains(".img"))
            {
                throw new ArgumentException("Supplied imgText is not legal.", nameof(imgText));
            }

            Wz_File outWzFile = null;

            // Filter map nodes, and find string wz file.
            IEnumerable<Wz_Node> mapNodes = root.Nodes
                .Where(n =>
                {
                    switch (n.GetNodeWzFile().Type)
                    {
                        case Wz_Type.Map:
                            return true;
                        case Wz_Type.String:
                            outWzFile = n.GetNodeWzFile();
                            break;
                        default:
                            break;
                    }
                    return false;
                });
            // Do search on each map node
            foreach (var mapRoot in mapNodes)
            {
                Wz_Node result = GenericBfsSearcher(mapRoot, (node) => node.Text == imgText);
                if (result != null)
                {
                    if (outWzFile == null)
                    {
                        throw new NullReferenceException("Cannot find Wz_File of type Wz_Type.String");
                    }
                    stringWzFile = outWzFile;
                    return result.GetNodeWzImage();
                }
            }
            // Throw if not found.
            throw new WzImgNotFoundException(string.Format("Target Img {0} cannot be found.", imgText));
        }

    }
}
