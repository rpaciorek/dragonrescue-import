
using System.Xml.Serialization;

namespace dragonrescue.Schema;

[XmlRoot(ElementName = "ArrayOfUserItemPositionSetRequest")]
[Serializable]
public class UserItemPositionSetRequestList {
    [XmlElement(ElementName = "UserItemPositionSetRequest")]
    public UserItemPosition[] UserItemPosition;
}
