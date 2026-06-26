using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleStory.Avatar.DebugCli
{
    public sealed class AvatarDebugCliOptions
    {
        public string MapleStoryPath { get; private set; } = string.Empty;

        public Encoding Encoding { get; private set; } = Encoding.UTF8;

        public IReadOnlyList<int> PartIds { get; private set; } = Array.Empty<int>();

        public string Action { get; private set; } = "stand1";

        public int BodyFrame { get; private set; }

        public string Emotion { get; private set; } = "default";

        public int EmotionFrame { get; private set; }

        public string TamingAction { get; private set; }

        public int TamingFrame { get; private set; }

        public int WeaponType { get; private set; }

        public int WeaponIndex { get; private set; }

        public string OutputPath { get; private set; } = string.Empty;

        public string OutputDirectory { get; private set; } = string.Empty;

        public bool ListActions { get; private set; }

        public bool ListEmotions { get; private set; }

        public bool AllBodyFrames { get; private set; }

        public bool AlignFrames { get; private set; }

        public static AvatarDebugCliOptions Parse(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            AvatarDebugCliOptions options = new AvatarDebugCliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--path":
                        options.MapleStoryPath = ReadValue(args, ref i, arg);
                        break;
                    case "--encoding":
                        options.Encoding = Encoding.GetEncoding(ReadValue(args, ref i, arg));
                        break;
                    case "--parts":
                        options.PartIds = ParsePartIds(ReadValue(args, ref i, arg));
                        break;
                    case "--action":
                        options.Action = ReadValue(args, ref i, arg);
                        break;
                    case "--body-frame":
                        options.BodyFrame = ParseNonNegativeInt(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--emotion":
                        options.Emotion = ReadValue(args, ref i, arg);
                        break;
                    case "--emotion-frame":
                        options.EmotionFrame = ParseNonNegativeInt(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--taming-action":
                        options.TamingAction = ReadValue(args, ref i, arg);
                        break;
                    case "--taming-frame":
                        options.TamingFrame = ParseNonNegativeInt(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--weapon-type":
                        options.WeaponType = ParseNonNegativeInt(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--weapon-index":
                        options.WeaponIndex = ParseNonNegativeInt(ReadValue(args, ref i, arg), arg);
                        break;
                    case "--out":
                        options.OutputPath = ReadValue(args, ref i, arg);
                        break;
                    case "--out-dir":
                        options.OutputDirectory = ReadValue(args, ref i, arg);
                        break;
                    case "--list-actions":
                        options.ListActions = true;
                        break;
                    case "--list-emotions":
                        options.ListEmotions = true;
                        break;
                    case "--all-body-frames":
                        options.AllBodyFrames = true;
                        break;
                    case "--align":
                        options.AlignFrames = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{arg}'.");
                }
            }

            options.Validate();
            return options;
        }

        public AvatarPose ToPose()
        {
            return new AvatarPose()
            {
                BodyAction = Action,
                BodyFrame = BodyFrame,
                Emotion = Emotion,
                EmotionFrame = EmotionFrame,
                TamingAction = TamingAction,
                TamingFrame = TamingFrame,
                WeaponType = WeaponType,
                WeaponIndex = WeaponIndex
            };
        }

        private static string ReadValue(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{optionName} requires a value.");
            }

            string value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{optionName} requires a non-empty value.");
            }

            return value;
        }

        private static IReadOnlyList<int> ParsePartIds(string value)
        {
            string[] parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException("--parts requires at least one ID.");
            }

            List<int> ids = new List<int>(parts.Length);
            foreach (string part in parts)
            {
                ids.Add(ParseNonNegativeInt(part.Trim(), "--parts"));
            }

            return ids;
        }

        private static int ParseNonNegativeInt(string value, string optionName)
        {
            if (!int.TryParse(value, out int result) || result < 0)
            {
                throw new ArgumentException($"{optionName} requires a non-negative integer.");
            }

            return result;
        }

        private void Validate()
        {
            if (ListActions)
            {
                return;
            }

            if (ListEmotions)
            {
                return;
            }

            if (PartIds.Count == 0)
            {
                throw new ArgumentException("--parts is required unless listing actions or emotions.");
            }

            if (AllBodyFrames)
            {
                if (string.IsNullOrWhiteSpace(OutputDirectory))
                {
                    throw new ArgumentException("--all-body-frames requires --out-dir.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                throw new ArgumentException("--out is required unless listing or using --all-body-frames.");
            }
        }
    }
}
