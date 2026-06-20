using UnityEngine;

namespace Pixygon.Passport {
    /// <summary>
    /// Profile-picture loader. The old implementation streamed the image from IPFS
    /// (Pixygon.Ipfs, via the retired nft package). That stack is gone; this is now a
    /// no-op placeholder that preserves the GetIcon/ClearIcon API so PassportCard and
    /// PassportBadge compile unchanged. Wire the replacement media pipeline in here.
    /// </summary>
    public class IconGetter : MonoBehaviour {
        [SerializeField] private Transform _parent;
        [SerializeField] private GameObject _loadObject;
        [SerializeField] private GameObject _defaultIcon;
        [SerializeField] private GameObject _spritebase;

        public void GetIcon(string hash, bool useDefault = false) {
            // IPFS image streaming removed with the nft/ipfs packages. Until the
            // replacement media system exists, show the default icon.
            if (_loadObject != null) _loadObject.SetActive(false);
            if (_defaultIcon != null) _defaultIcon.SetActive(true);
        }

        public void ClearIcon() {
            if (_loadObject != null) _loadObject.SetActive(false);
        }
    }
}
