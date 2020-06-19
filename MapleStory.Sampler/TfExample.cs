using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapRender.Invoker;
using ProtoBuf;
using Tensorflow;

namespace MapleStory.Sampler
{
    public class TfExample
    {
        #region KEYS
        private const string KeyHeight = @"image/height";
        private const string KeyWidth = @"image/width";
        private const string KeyFileName = @"image/filename";
        private const string KeySourceId = @"image/source_id";
        private const string KeyEncoded = @"image/encoded";
        private const string KeyFormat = @"image/format";
        private const string KeyMinX = @"image/object/bbox/xmin";
        private const string KeyMaxX = @"image/object/bbox/xmax";
        private const string KeyMinY = @"image/object/bbox/ymin";
        private const string KeyMaxY = @"image/object/bbox/ymax";
        private const string KeyClassText = @"image/object/class/text";
        private const string KeyLabel = @"image/object/class/label";
        #endregion

        private const string JpegFormatString = @"jpeg";

        private Example _underlyingExample;
        private Features _features;
        private Dictionary<string, Feature> _featureMap;
        private List<float> _minXList;
        private List<float> _minYList;
        private List<float> _maxXList;
        private List<float> _maxYList;
        private List<byte[]> _classTextList;
        private List<long> _labelList;
        private float _sampleWidth;
        private float _sampleHeight;

        private TfExample(MemoryStream imageStream, int width, int height, int itemCount)
        {
            _underlyingExample = new Example();
            _underlyingExample.Features = new Features();
            _features = _underlyingExample.Features;
            _featureMap = _features.feature;
            _sampleHeight = height;
            _sampleWidth = width;

            _minXList = new List<float>(itemCount);
            _minYList = new List<float>(itemCount);
            _maxXList = new List<float>(itemCount);
            _maxYList = new List<float>(itemCount);
            _classTextList = new List<byte[]>(itemCount);
            _labelList = new List<long>(itemCount);

            _featureMap[KeyFormat] = NewBytesFeature(JpegFormatString);
            _featureMap[KeyHeight] = NewInt64Feature(height);
            _featureMap[KeyWidth] = NewInt64Feature(width);
            _featureMap[KeyEncoded] = NewBytesFeature(imageStream.ToArray());
            var guid = Guid.NewGuid().ToString();
            _featureMap[KeyFileName] = NewBytesFeature(guid);
            _featureMap[KeySourceId] = NewBytesFeature(guid);
        }

        public static TfExample From(MemoryStream imageStream, IEnumerable<TargetItem> items, int width, int height)
        {
            TfExample ret = new TfExample(imageStream, width, height, items.Count());
            foreach (var item in items)
            {
                ret.AddItem(item);
            }
            return ret;
        }

        public MemoryStream SerializeToStream()
        {
            _featureMap[KeyMinX] = NewFloatListFeature(_minXList.ToArray());
            _featureMap[KeyMaxX] = NewFloatListFeature(_maxXList.ToArray());
            _featureMap[KeyMinY] = NewFloatListFeature(_minYList.ToArray());
            _featureMap[KeyMaxY] = NewFloatListFeature(_maxYList.ToArray());
            _featureMap[KeyClassText] = NewBytesListFeature(_classTextList);
            _featureMap[KeyLabel] = NewInt64ListFeature(_labelList.ToArray());

            MemoryStream ret = new MemoryStream();
            Serializer.Serialize(ret, _underlyingExample);
            return ret;
        }

        private void AddItem(TargetItem item)
        {
            float x = item.X;
            float y = item.Y;
            float x2 = x + item.Width;
            float y2 = y + item.Height;
            _minXList.Add(x / _sampleWidth);
            _maxXList.Add(x2 / _sampleWidth);

            _minYList.Add(y / _sampleHeight);
            _maxYList.Add(y2 / _sampleHeight);

            if (item.Type == MapRender.Invoker.ObjectClass.Unknown || !Enum.IsDefined(typeof(ObjectClass), item.Type))
            {
                throw new ArgumentException("TargetItem contains unknown type item!");
            }
            _classTextList.Add(Encoding.UTF8.GetBytes(Enum.GetName(typeof(ObjectClass), item.Type)));
            _labelList.Add((long)item.Type);
        }

        private static Feature NewBytesListFeature(IEnumerable<byte[]> value)
        {
            var bytesList = new BytesList();
            bytesList.Values.AddRange(value);
            return new Feature() { BytesList = bytesList };
        }

        private static Feature NewFloatListFeature(float[] value)
        {
            return new Feature() { FloatList = new FloatList() { Values = value } };
        }

        private static Feature NewInt64ListFeature(long[] value)
        {
            return new Feature() { Int64List = new Int64List() { Values = value } };
        }

        private static Feature NewInt64Feature(long value)
        {
            return new Feature() { Int64List = new Int64List() { Values = new[] { value } } };
        }

        private static Feature NewBytesFeature(byte[] value)
        {
            return new Feature() { BytesList = new BytesList() { Values = { value } } };
        }

        private static Feature NewBytesFeature(string text)
        {
            return NewBytesFeature(Encoding.UTF8.GetBytes(text));
        }
    }
}
