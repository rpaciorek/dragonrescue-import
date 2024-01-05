using System.Xml.Serialization;

namespace dragonrescue.Schema;

[XmlRoot(ElementName = "UserItem", Namespace = "")]
[Serializable]
public class UserItemData
{
	[XmlElement(ElementName = "iid")]
	public int ItemID { get; set; }

	[XmlElement(ElementName = "md", IsNullable = true)]
	public DateTime? ModifiedDate { get; set; }

	[XmlElement(ElementName = "uia", IsNullable = true)]
	public PairData UserItemAttributes { get; set; }

	[XmlElement(ElementName = "iss", IsNullable = true)]
	public ItemStat[] ItemStats { get; set; }

	[XmlElement(ElementName = "IT", IsNullable = true)]
	public ItemTier? ItemTier { get; set; }

	[XmlElement(ElementName = "cd", IsNullable = true)]
	public DateTime? CreatedDate { get; set; }

	[XmlElement(ElementName = "uiid")]
	public int UserInventoryID;

	[XmlElement(ElementName = "q")]
	public int Quantity;

	[XmlElement(ElementName = "u")]
	public int Uses;

	[XmlElement(ElementName = "i")]
	public ItemData Item;
}
