using System.Xml;
using System.Xml.Serialization;

namespace Eventor.Api
{
    internal class XmlParser
    {
       private static readonly Dictionary<Type, XmlSerializer> _xmlSerializers = new();

        private static XmlReaderSettings _readerSettings = new() { Async = true };

        public static T Parse<T>(Stream xml)
        {
            var serializer = GetSerializerForType(typeof(T));
            using var xmlReader = XmlReader.Create(xml, _readerSettings);
            var parsed = serializer.Deserialize(xmlReader);

            return parsed != null ? (T)parsed : throw new InvalidOperationException("Tried to parse xml strongly typed, but the result was null.");
        }

        private static XmlSerializer GetSerializerForType(Type objectType)
        {
            lock (_xmlSerializers)
            {
                // Creating serializers is an expensive operation. Create only one instance per type.
                XmlSerializer serializer;
                if (_xmlSerializers.TryGetValue(objectType, out XmlSerializer? value))
                {
                    serializer = value;
                }
                else
                {
                    serializer = new XmlSerializer(objectType);
                    _xmlSerializers.Add(objectType, serializer);
                }
                return serializer;
            }
        }
    }
}
