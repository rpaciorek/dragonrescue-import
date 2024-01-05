using System.Xml.Serialization;

namespace dragonrescue.Schema;

public class BluePrintSpecification
{
	[XmlElement(ElementName = "BPSID", IsNullable = false)]
	public int BluePrintSpecID { get; set; }

	[XmlElement(ElementName = "BPIID", IsNullable = false)]
	public int BluePrintItemID { get; set; }

	[XmlElement(ElementName = "IID", IsNullable = true)]
	public int? ItemID { get; set; }

	[XmlElement(ElementName = "CID", IsNullable = true)]
	public int? CategoryID { get; set; }

	[XmlElement(ElementName = "IR", IsNullable = true)]
	public ItemRarity? ItemRarity { get; set; }

	[XmlElement(ElementName = "T", IsNullable = true)]
	public ItemTier? Tier { get; set; }

	[XmlElement(ElementName = "QTY", IsNullable = false)]
	public int Quantity { get; set; }

	[XmlElement(ElementName = "ST", IsNullable = false)]
	public SpecificationType SpecificationType { get; set; }
}
