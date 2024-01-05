using System.Xml.Serialization;

namespace dragonrescue.Schema;

[XmlRoot(ElementName = "ABIR", Namespace = "")]
[Serializable]
public class AddBattleItemsRequest {
	[XmlElement(ElementName = "BITM", IsNullable = false)]
	public List<BattleItemTierMap> BattleItemTierMaps { get; set; }
}
