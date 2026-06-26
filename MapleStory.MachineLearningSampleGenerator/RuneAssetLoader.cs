using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using MapleStory.Common;
using MapleStory.Sampler.PostProcessor;
using WzComparerR2;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class RuneAssetLoader
    {
        private static readonly IReadOnlyDictionary<string, RuneArrowDirection> DirectionMap =
            new Dictionary<string, RuneArrowDirection>(StringComparer.Ordinal)
            {
                { "0", RuneArrowDirection.Down },
                { "1", RuneArrowDirection.Up },
                { "2", RuneArrowDirection.Left },
                { "3", RuneArrowDirection.Right },
            };

        public static RuneAssetSet Load(string mapleStoryPath, Encoding encoding)
        {
            using WzContext context = new WzContext(mapleStoryPath, encoding, false);
            context.Activate();

            Wz_Node runeStone = PluginManager.FindWz(@"Etc\RuneStone.img");
            if (runeStone == null)
            {
                throw new ConfigurationException("Rune asset path 'Etc/RuneStone.img' could not be found.");
            }
            runeStone = ExtractImageNode(runeStone, "Etc/RuneStone.img");

            List<RuneArrowAsset> arrows = LoadArrows(runeStone);
            if (arrows.Count == 0)
            {
                throw new ConfigurationException("Rune asset path 'Etc/RuneStone.img/arrow*' did not contain any usable arrows.");
            }

            List<Bitmap> noises = LoadNoises(runeStone.Nodes["Noise"]);
            return new RuneAssetSet(arrows, noises);
        }

        private static List<RuneArrowAsset> LoadArrows(Wz_Node runeStone)
        {
            List<RuneArrowAsset> arrows = new List<RuneArrowAsset>();
            foreach (Wz_Node arrowNode in EnumerateDescendants(runeStone)
                .Where(node => node.Text.StartsWith("arrow", StringComparison.OrdinalIgnoreCase)))
            {
                Wz_Node extractedArrowNode = ExtractImageNode(arrowNode, arrowNode.FullPath);
                List<Bitmap> baseTemplates = LoadBases(extractedArrowNode);
                try
                {
                    List<(string Key, RuneArrowDirection Direction, Wz_Node Node)> directionNodes = DirectionMap
                        .Select(entry => (entry.Key, entry.Value, Node: extractedArrowNode.Nodes[entry.Key]))
                        .Where(entry => entry.Node != null)
                        .ToList();

                    bool singleZeroArrow = directionNodes.Count == 1 && directionNodes[0].Key == "0";
                    foreach ((string key, RuneArrowDirection direction, Wz_Node node) in directionNodes)
                    {
                        Bitmap arrowBitmap = TryLoadBitmap(FindPreferredArrowFrame(node));
                        if (arrowBitmap == null)
                        {
                            continue;
                        }

                        RuneArrowDirection resolvedDirection = singleZeroArrow ? RuneArrowDirection.Left : direction;
                        arrows.Add(new RuneArrowAsset(
                            $"{extractedArrowNode.Text}/{key}",
                            resolvedDirection,
                            arrowBitmap,
                            baseTemplates.Select(CloneBitmap)));
                    }
                }
                finally
                {
                    foreach (Bitmap bitmap in baseTemplates)
                    {
                        bitmap.Dispose();
                    }
                }
            }

            return arrows;
        }

        private static Wz_Node FindPreferredArrowFrame(Wz_Node directionNode)
        {
            if (directionNode == null)
            {
                return null;
            }

            Wz_Node stayFrame = directionNode.Nodes["stay"]?.Nodes["0"];
            if (stayFrame != null)
            {
                return stayFrame;
            }

            return EnumerateDescendants(directionNode).FirstOrDefault(node => node.Value is Wz_Png)
                ?? directionNode;
        }

        private static Wz_Node ExtractImageNode(Wz_Node node, string context)
        {
            Wz_Image image = node.GetValue<Wz_Image>(null);
            if (image == null)
            {
                return node;
            }

            Exception exception;
            image.TryExtract(out exception);
            if (exception != null)
            {
                throw new ConfigurationException($"Rune asset path '{context}' could not be extracted: {exception.Message}");
            }

            return image.Node;
        }

        private static List<Bitmap> LoadBases(Wz_Node arrowNode)
        {
            List<Bitmap> bases = new List<Bitmap>();
            Bitmap baseBitmap = TryLoadBitmap(arrowNode.Nodes["base"]);
            if (baseBitmap != null)
            {
                bases.Add(baseBitmap);
            }

            Bitmap base2Bitmap = TryLoadBitmap(arrowNode.Nodes["base2"]);
            if (base2Bitmap != null)
            {
                bases.Add(base2Bitmap);
            }

            return bases;
        }

        private static List<Bitmap> LoadNoises(Wz_Node noiseRoot)
        {
            List<Bitmap> noises = new List<Bitmap>();
            if (noiseRoot == null)
            {
                return noises;
            }

            Queue<Wz_Node> queue = new Queue<Wz_Node>();
            queue.Enqueue(noiseRoot);
            while (queue.Count > 0)
            {
                Wz_Node node = queue.Dequeue();
                Bitmap bitmap = TryLoadBitmap(node);
                if (bitmap != null)
                {
                    noises.Add(bitmap);
                }

                foreach (Wz_Node child in node.Nodes)
                {
                    queue.Enqueue(child);
                }
            }

            return noises;
        }

        private static IEnumerable<Wz_Node> EnumerateDescendants(Wz_Node root)
        {
            Queue<Wz_Node> queue = new Queue<Wz_Node>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Wz_Node node = ExtractImageNode(queue.Dequeue(), root.FullPath);
                yield return node;

                foreach (Wz_Node child in node.Nodes)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private static Bitmap TryLoadBitmap(Wz_Node node)
        {
            if (node == null)
            {
                return null;
            }

            try
            {
                BitmapOrigin bitmapOrigin = BitmapOrigin.CreateFromNode(node, PluginManager.FindWz);
                return bitmapOrigin.Bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap CloneBitmap(Bitmap bitmap)
        {
            return new Bitmap(bitmap);
        }
    }
}
