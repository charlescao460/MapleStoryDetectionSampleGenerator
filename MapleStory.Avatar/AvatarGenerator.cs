using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using MapleStory.Common;
using WzComparerR2;
using WzComparerR2.CharaSim;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapleStory.Avatar
{
    public sealed class AvatarGenerator : IDisposable
    {
        private readonly WzContext _wzContext;
        private bool _disposed;

        public AvatarGenerator(string mapleStoryPath)
            : this(mapleStoryPath, Encoding.UTF8, false)
        {
        }

        public AvatarGenerator(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
        {
            _wzContext = new WzContext(mapleStoryPath, encoding, disableImgCheck);
        }

        public IReadOnlyList<string> GetAvailableActions()
        {
            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(AvatarAppearance.Empty);
            return canvas.Actions.Select(action => action.Name).ToArray();
        }

        public IReadOnlyList<string> GetAvailableEmotions(AvatarAppearance appearance = null)
        {
            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance ?? AvatarAppearance.Empty);
            return canvas.Emotions.ToArray();
        }

        public int GetBodyFrameCount(AvatarAppearance appearance, string action)
        {
            if (appearance == null)
            {
                throw new ArgumentNullException(nameof(appearance));
            }

            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance);
            return GetRequiredBodyFrames(canvas, action).Length;
        }

        public int GetEmotionFrameCount(AvatarAppearance appearance, string emotion)
        {
            if (appearance == null)
            {
                throw new ArgumentNullException(nameof(appearance));
            }

            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance);
            return GetEmotionFrames(canvas, emotion).Length;
        }

        public AvatarFrameResult RenderFrame(AvatarAppearance appearance, AvatarPose pose)
        {
            if (appearance == null)
            {
                throw new ArgumentNullException(nameof(appearance));
            }

            if (pose == null)
            {
                throw new ArgumentNullException(nameof(pose));
            }

            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance);
            using (RenderedAvatarFrame frame = RenderFrameData(canvas, pose, pose.BodyFrame, pose.EmotionFrame, pose.TamingFrame))
            {
                return EncodeFrame(frame);
            }
        }

        public AvatarFrameResult SaveFrame(AvatarAppearance appearance, AvatarPose pose, string path)
        {
            AvatarFrameResult result = RenderFrame(appearance, pose);
            EnsureParentDirectory(path);
            result.Save(path);
            return result;
        }

        public IReadOnlyList<AvatarFrameResult> RenderBodyAnimationFrames(
            AvatarAppearance appearance,
            AvatarPose pose,
            bool alignFrames = false)
        {
            if (appearance == null)
            {
                throw new ArgumentNullException(nameof(appearance));
            }

            if (pose == null)
            {
                throw new ArgumentNullException(nameof(pose));
            }

            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance);
            WzComparerR2.Avatar.ActionFrame[] bodyFrames = GetRequiredBodyFrames(canvas, pose.BodyAction);
            ValidateFrameIndex("emotion", pose.Emotion, pose.EmotionFrame, GetEmotionFrames(canvas, pose.Emotion));
            ValidateTamingSelection(canvas, pose);

            List<RenderedAvatarFrame> frames = new List<RenderedAvatarFrame>(bodyFrames.Length);
            try
            {
                for (int i = 0; i < bodyFrames.Length; i++)
                {
                    frames.Add(RenderFrameData(canvas, pose, i, pose.EmotionFrame, pose.TamingFrame));
                }

                return EncodeFrames(frames, alignFrames);
            }
            finally
            {
                foreach (RenderedAvatarFrame frame in frames)
                {
                    frame.Dispose();
                }
            }
        }

        public IReadOnlyList<AvatarFrameResult> RenderEmotionAnimationFrames(
            AvatarAppearance appearance,
            AvatarPose pose,
            bool alignFrames = false)
        {
            if (appearance == null)
            {
                throw new ArgumentNullException(nameof(appearance));
            }

            if (pose == null)
            {
                throw new ArgumentNullException(nameof(pose));
            }

            WzComparerR2.Avatar.AvatarCanvas canvas = CreateCanvas(appearance);
            WzComparerR2.Avatar.ActionFrame[] emotionFrames = GetEmotionFrames(canvas, pose.Emotion);
            if (emotionFrames.Length == 0)
            {
                throw new InvalidOperationException($"Emotion '{pose.Emotion}' has no frames for the current appearance.");
            }

            ValidateFrameIndex("body action", pose.BodyAction, pose.BodyFrame, GetRequiredBodyFrames(canvas, pose.BodyAction));
            ValidateTamingSelection(canvas, pose);

            List<RenderedAvatarFrame> frames = new List<RenderedAvatarFrame>(emotionFrames.Length);
            try
            {
                for (int i = 0; i < emotionFrames.Length; i++)
                {
                    frames.Add(RenderFrameData(canvas, pose, pose.BodyFrame, i, pose.TamingFrame));
                }

                return EncodeFrames(frames, alignFrames);
            }
            finally
            {
                foreach (RenderedAvatarFrame frame in frames)
                {
                    frame.Dispose();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _wzContext.Dispose();
            _disposed = true;
        }

        private WzComparerR2.Avatar.AvatarCanvas CreateCanvas(AvatarAppearance appearance)
        {
            ThrowIfDisposed();
            _wzContext.Activate();

            WzComparerR2.Avatar.AvatarCanvas canvas = new WzComparerR2.Avatar.AvatarCanvas();
            if (!canvas.LoadZ())
            {
                throw new InvalidOperationException("Avatar z-map resource Base\\zmap.img could not be loaded.");
            }

            if (!canvas.LoadActions())
            {
                throw new InvalidOperationException("Avatar body action resource Character\\00002000.img could not be loaded.");
            }

            ApplyAppearance(canvas, appearance);

            if (!canvas.LoadEmotions())
            {
                throw new InvalidOperationException("Avatar emotion resources could not be loaded.");
            }

            if (canvas.Taming != null)
            {
                canvas.LoadTamingActions();
            }

            return canvas;
        }

        private void ApplyAppearance(WzComparerR2.Avatar.AvatarCanvas canvas, AvatarAppearance appearance)
        {
            bool hasExplicitBody = false;
            bool hasExplicitHead = false;

            foreach (int partId in appearance.PartIds)
            {
                Wz_Node imgNode = FindNodeByGearId(partId);
                if (imgNode == null)
                {
                    throw new AvatarPartNotFoundException(partId);
                }

                WzComparerR2.Avatar.AvatarPart part = AddPart(canvas, imgNode);
                if (part == null)
                {
                    throw new InvalidOperationException($"Avatar part {partId:D8}.img does not contain a usable info node.");
                }

                GearType gearType = Gear.GetGearType(part.ID.Value);
                if (gearType == GearType.body)
                {
                    hasExplicitBody = true;
                }
                else if (gearType == GearType.head)
                {
                    hasExplicitHead = true;
                }

                ApplyNewPartSideEffects(canvas, part, syncBodyAndHead: false);
            }

            if (!hasExplicitHead && canvas.Body?.ID != null)
            {
                AddResolvedPartIfFound(canvas, 10000 + canvas.Body.ID.Value % 10000);
            }

            if (!hasExplicitBody && canvas.Head?.ID != null)
            {
                AddResolvedPartIfFound(canvas, canvas.Head.ID.Value % 10000);
            }
        }

        private void ApplyNewPartSideEffects(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            WzComparerR2.Avatar.AvatarPart part,
            bool syncBodyAndHead)
        {
            if (part.ID == null)
            {
                return;
            }

            if (syncBodyAndHead && part == canvas.Body)
            {
                int headId = 10000 + part.ID.Value % 10000;
                if (canvas.Head == null || canvas.Head.ID != headId)
                {
                    AddResolvedPartIfFound(canvas, headId);
                }
            }
            else if (syncBodyAndHead && part == canvas.Head)
            {
                int bodyId = part.ID.Value % 10000;
                if (canvas.Body == null || canvas.Body.ID != bodyId)
                {
                    AddResolvedPartIfFound(canvas, bodyId);
                }
            }
            else if (part == canvas.Taming)
            {
                canvas.LoadTamingActions();
            }
            else if (part == canvas.Pants || part == canvas.Coat)
            {
                if (canvas.Longcoat != null)
                {
                    canvas.Longcoat.Visible = false;
                }
            }
            else if (part == canvas.Longcoat)
            {
                if (canvas.Pants != null && canvas.Pants.Visible || canvas.Coat != null && canvas.Coat.Visible)
                {
                    canvas.Longcoat.Visible = false;
                }
            }
        }

        private void AddResolvedPartIfFound(WzComparerR2.Avatar.AvatarCanvas canvas, int partId)
        {
            Wz_Node node = FindNodeByGearId(partId);
            if (node != null)
            {
                AddPart(canvas, node);
            }
        }

        private static WzComparerR2.Avatar.AvatarPart AddPart(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            Wz_Node imgNode)
        {
            if (imgNode.FindNodeByPath("info") != null)
            {
                return canvas.AddPart(imgNode);
            }

            WzComparerR2.Avatar.AvatarPart part = new WzComparerR2.Avatar.AvatarPart(imgNode);
            if (part.ID == null)
            {
                return null;
            }

            switch (Gear.GetGearType(part.ID.Value))
            {
                case GearType.body:
                    canvas.Body = part;
                    return part;
                case GearType.head:
                    canvas.Head = part;
                    return part;
                case GearType.face:
                case GearType.face2:
                    canvas.Face = part;
                    return part;
                case GearType.hair:
                case GearType.hair2:
                case GearType.hair3:
                case GearType.hair4:
                    canvas.Hair = part;
                    return part;
                default:
                    return null;
            }
        }

        private Wz_Node FindNodeByGearId(int partId)
        {
            string imgName = partId.ToString("D8") + ".img";
            Wz_Node characterWz = PluginManager.FindWz(Wz_Type.Character);
            if (characterWz == null)
            {
                throw new InvalidOperationException("Character.wz could not be loaded from the active MapleStory WZ context.");
            }

            List<Wz_Node> matches = new List<Wz_Node>();
            CollectImgNodes(characterWz, imgName, matches);
            Wz_Node imgNode = matches.FirstOrDefault(node => !IsCanvasNode(node)) ?? matches.FirstOrDefault();

            if (imgNode == null)
            {
                return null;
            }

            Wz_Image img = imgNode.GetValue<Wz_Image>();
            if (img == null)
            {
                return null;
            }

            Exception exception;
            if (!img.TryExtract(out exception))
            {
                throw new InvalidOperationException($"Avatar part {partId:D8}.img could not be extracted.", exception);
            }

            return img.Node;
        }

        private static void CollectImgNodes(Wz_Node node, string imgName, ICollection<Wz_Node> matches)
        {
            if (node.Text == imgName && node.GetValueEx<Wz_Image>(null) != null)
            {
                matches.Add(node);
            }

            foreach (Wz_Node child in node.Nodes)
            {
                CollectImgNodes(child, imgName, matches);
            }
        }

        private static bool IsCanvasNode(Wz_Node node)
        {
            Wz_Node current = node.ParentNode;
            while (current != null)
            {
                if (string.Equals(current.Text, "_Canvas", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.ParentNode;
            }

            return false;
        }

        private RenderedAvatarFrame RenderFrameData(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            AvatarPose pose,
            int bodyFrame,
            int emotionFrame,
            int tamingFrame)
        {
            ApplyPose(canvas, pose);

            WzComparerR2.Avatar.ActionFrame[] bodyFrames = GetRequiredBodyFrames(canvas, pose.BodyAction);
            ValidateFrameIndex("body action", pose.BodyAction, bodyFrame, bodyFrames);

            WzComparerR2.Avatar.ActionFrame[] emotionFrames = GetEmotionFrames(canvas, pose.Emotion);
            ValidateFrameIndex("emotion", pose.Emotion, emotionFrame, emotionFrames);

            WzComparerR2.Avatar.ActionFrame[] tamingFrames = GetTamingFrames(canvas, pose);
            ValidateFrameIndex("taming action", canvas.TamingActionName, tamingFrame, tamingFrames);

            WzComparerR2.Avatar.Bone bone = canvas.CreateFrame(bodyFrame, emotionFrame, tamingFrame);
            BitmapOrigin bitmapOrigin = canvas.DrawFrame(bone);
            if (bitmapOrigin.Bitmap == null || bitmapOrigin.Bitmap.Width == 0 || bitmapOrigin.Bitmap.Height == 0)
            {
                throw new InvalidOperationException("Avatar frame rendered an empty bitmap.");
            }

            BodyFrameBounds bodyBounds = MeasureBodyBounds(canvas, bone);
            return new RenderedAvatarFrame(
                bitmapOrigin,
                bodyBounds.Origin,
                bodyBounds.Width,
                bodyBounds.Height,
                bodyFrames[bodyFrame].AbsoluteDelay,
                emotionFrames.Length > 0 ? emotionFrames[emotionFrame].AbsoluteDelay : 0,
                tamingFrames.Length > 0 ? tamingFrames[tamingFrame].AbsoluteDelay : 0);
        }

        private static BodyFrameBounds MeasureBodyBounds(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            WzComparerR2.Avatar.Bone bone)
        {
            RemoveNonBodyBoundingSkins(bone);

            BitmapOrigin bodyFrame = canvas.DrawFrame(bone);
            try
            {
                // DrawFrame returns a null bitmap when no layers remain, which can happen for
                // effect-only or weapon-only appearances after non-body skins are excluded.
                if (bodyFrame.Bitmap == null)
                {
                    return BodyFrameBounds.Empty;
                }

                return new BodyFrameBounds(bodyFrame.Origin, bodyFrame.Bitmap.Width, bodyFrame.Bitmap.Height);
            }
            finally
            {
                bodyFrame.Bitmap?.Dispose();
            }
        }

        private static void RemoveNonBodyBoundingSkins(WzComparerR2.Avatar.Bone bone)
        {
            bone.Skins.RemoveAll(skin => IsNonBodyBoundingSkin(skin.Name));
            foreach (WzComparerR2.Avatar.Bone child in bone.Children)
            {
                RemoveNonBodyBoundingSkins(child);
            }
        }

        private static bool IsNonBodyBoundingSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName))
            {
                return false;
            }

            return skinName.StartsWith("weapon", StringComparison.OrdinalIgnoreCase)
                || skinName.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0
                || skinName.IndexOf("afterimage", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyPose(WzComparerR2.Avatar.AvatarCanvas canvas, AvatarPose pose)
        {
            canvas.ActionName = pose.BodyAction;
            canvas.EmotionName = pose.Emotion;
            canvas.TamingActionName = ResolveTamingAction(canvas, pose);
            canvas.WeaponType = ResolveWeaponType(canvas, pose.WeaponType);
            canvas.WeaponIndex = pose.WeaponIndex;
            canvas.EarType = pose.EarType;
            canvas.HairCover = pose.HairCover;
            canvas.ShowHairShade = pose.ShowHairShade;
        }

        private static int ResolveWeaponType(WzComparerR2.Avatar.AvatarCanvas canvas, int requestedWeaponType)
        {
            List<int> weaponTypes = canvas.GetCashWeaponTypes();
            if (weaponTypes.Count == 0)
            {
                return requestedWeaponType;
            }

            if (weaponTypes.Contains(requestedWeaponType))
            {
                return requestedWeaponType;
            }

            if (requestedWeaponType == 0)
            {
                return weaponTypes[0];
            }

            throw new ArgumentOutOfRangeException(
                nameof(requestedWeaponType),
                requestedWeaponType,
                $"Cash weapon {canvas.Weapon?.ID:D8} supports weapon types: {string.Join(", ", weaponTypes)}.");
        }

        private static string ResolveTamingAction(WzComparerR2.Avatar.AvatarCanvas canvas, AvatarPose pose)
        {
            if (canvas.Taming == null)
            {
                return pose.TamingAction;
            }

            if (!string.IsNullOrEmpty(pose.TamingAction))
            {
                return pose.TamingAction;
            }

            if (canvas.TamingActions.Contains("stand1"))
            {
                return "stand1";
            }

            return canvas.TamingActions.Count > 0 ? canvas.TamingActions[0] : null;
        }

        private static WzComparerR2.Avatar.ActionFrame[] GetRequiredBodyFrames(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Body action is required.", nameof(action));
            }

            WzComparerR2.Avatar.ActionFrame[] frames = canvas.GetActionFrames(action);
            if (frames.Length == 0)
            {
                throw new InvalidOperationException($"Body action '{action}' has no frames.");
            }

            return frames;
        }

        private static WzComparerR2.Avatar.ActionFrame[] GetEmotionFrames(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            string emotion)
        {
            if (string.IsNullOrWhiteSpace(emotion))
            {
                return Array.Empty<WzComparerR2.Avatar.ActionFrame>();
            }

            WzComparerR2.Avatar.ActionFrame[] frames = canvas.GetFaceFrames(emotion);
            if (canvas.Face != null && frames.Length == 0)
            {
                throw new InvalidOperationException($"Emotion '{emotion}' has no frames for the current face.");
            }

            return frames;
        }

        private static WzComparerR2.Avatar.ActionFrame[] GetTamingFrames(
            WzComparerR2.Avatar.AvatarCanvas canvas,
            AvatarPose pose)
        {
            if (canvas.Taming == null)
            {
                return Array.Empty<WzComparerR2.Avatar.ActionFrame>();
            }

            string tamingAction = ResolveTamingAction(canvas, pose);
            if (string.IsNullOrWhiteSpace(tamingAction))
            {
                throw new InvalidOperationException("The current taming part has no usable actions.");
            }

            WzComparerR2.Avatar.ActionFrame[] frames = canvas.GetTamingFrames(tamingAction);
            if (frames.Length == 0)
            {
                throw new InvalidOperationException($"Taming action '{tamingAction}' has no frames.");
            }

            return frames;
        }

        private static void ValidateTamingSelection(WzComparerR2.Avatar.AvatarCanvas canvas, AvatarPose pose)
        {
            ValidateFrameIndex("taming action", ResolveTamingAction(canvas, pose), pose.TamingFrame, GetTamingFrames(canvas, pose));
        }

        private static void ValidateFrameIndex(
            string frameKind,
            string actionName,
            int frameIndex,
            WzComparerR2.Avatar.ActionFrame[] frames)
        {
            if (frames.Length == 0)
            {
                if (frameIndex == 0)
                {
                    return;
                }

                throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"{frameKind} '{actionName}' has no frames.");
            }

            if (frameIndex < 0 || frameIndex >= frames.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameIndex),
                    frameIndex,
                    $"{frameKind} '{actionName}' frame index must be between 0 and {frames.Length - 1}.");
            }
        }

        private static IReadOnlyList<AvatarFrameResult> EncodeFrames(
            IReadOnlyList<RenderedAvatarFrame> frames,
            bool alignFrames)
        {
            if (!alignFrames)
            {
                return frames.Select(EncodeFrame).ToArray();
            }

            Rectangle canvasRect = frames
                .Select(frame => frame.BitmapOrigin.Rectangle)
                .Aggregate(Rectangle.Empty, (current, next) =>
                {
                    if (current.IsEmpty)
                    {
                        return next;
                    }

                    return next.IsEmpty ? current : Rectangle.Union(current, next);
                });

            if (canvasRect.IsEmpty)
            {
                return Array.Empty<AvatarFrameResult>();
            }

            List<AvatarFrameResult> results = new List<AvatarFrameResult>(frames.Count);
            foreach (RenderedAvatarFrame frame in frames)
            {
                using (Bitmap bitmap = new Bitmap(canvasRect.Width, canvasRect.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        Point drawPoint = new Point(
                            frame.BitmapOrigin.OpOrigin.X - canvasRect.X,
                            frame.BitmapOrigin.OpOrigin.Y - canvasRect.Y);
                        graphics.DrawImage(frame.BitmapOrigin.Bitmap, drawPoint);
                    }

                    BitmapOrigin alignedFrame = new BitmapOrigin(bitmap, -canvasRect.X, -canvasRect.Y);
                    results.Add(CreateResult(
                        alignedFrame,
                        frame.BodyOrigin,
                        frame.BodyWidth,
                        frame.BodyHeight,
                        frame.BodyDelay,
                        frame.EmotionDelay,
                        frame.TamingDelay));
                }
            }

            return results;
        }

        private static AvatarFrameResult EncodeFrame(RenderedAvatarFrame frame)
        {
            return CreateResult(
                frame.BitmapOrigin,
                frame.BodyOrigin,
                frame.BodyWidth,
                frame.BodyHeight,
                frame.BodyDelay,
                frame.EmotionDelay,
                frame.TamingDelay);
        }

        private static AvatarFrameResult CreateResult(
            BitmapOrigin frame,
            Point bodyOrigin,
            int bodyWidth,
            int bodyHeight,
            int bodyDelay,
            int emotionDelay,
            int tamingDelay)
        {
            return new AvatarFrameResult(
                frame.Bitmap,
                frame.Origin,
                bodyOrigin,
                bodyWidth,
                bodyHeight,
                bodyDelay,
                emotionDelay,
                tamingDelay);
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AvatarGenerator));
            }
        }

        private sealed class RenderedAvatarFrame : IDisposable
        {
            public RenderedAvatarFrame(
                BitmapOrigin bitmapOrigin,
                Point bodyOrigin,
                int bodyWidth,
                int bodyHeight,
                int bodyDelay,
                int emotionDelay,
                int tamingDelay)
            {
                BitmapOrigin = bitmapOrigin;
                BodyOrigin = bodyOrigin;
                BodyWidth = bodyWidth;
                BodyHeight = bodyHeight;
                BodyDelay = bodyDelay;
                EmotionDelay = emotionDelay;
                TamingDelay = tamingDelay;
            }

            public BitmapOrigin BitmapOrigin { get; }

            public Point BodyOrigin { get; }

            public int BodyWidth { get; }

            public int BodyHeight { get; }

            public int BodyDelay { get; }

            public int EmotionDelay { get; }

            public int TamingDelay { get; }

            public void Dispose()
            {
                BitmapOrigin.Bitmap?.Dispose();
            }
        }

        private readonly struct BodyFrameBounds
        {
            public static BodyFrameBounds Empty { get; } = new BodyFrameBounds(Point.Empty, 0, 0);

            public BodyFrameBounds(Point origin, int width, int height)
            {
                Origin = origin;
                Width = width;
                Height = height;
            }

            public Point Origin { get; }

            public int Width { get; }

            public int Height { get; }
        }
    }
}
