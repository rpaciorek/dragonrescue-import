using System.Xml.Serialization;

namespace dragonrescue.Schema;

[XmlRoot(ElementName = "UISS", Namespace = "")]
[Serializable]
public class UserItemStat {
    [XmlElement(ElementName = "iss", IsNullable = true)]
    public ItemStat[] ItemStats { get; set; }

    [XmlElement(ElementName = "it", IsNullable = true)]
    public ItemTier? ItemTier { get; set; }
}
