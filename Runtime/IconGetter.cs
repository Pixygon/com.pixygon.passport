using Pixygon.Ipfs;
using UnityEngine;

namespace Pixygon.Passport {
    public class IconGetter : MonoBehaviour {
        [SerializeField] private Transform _parent;
        [SerializeField] private GameObject _loadObject;
        [SerializeField] private GameObject _defaultIcon;
        [SerializeField] private GameObject _spritebase;
        private GameObject _go;
        
        public async void GetIcon(string hash, bool useDefault = false) {
            ClearIcon();
            Debug.Log("Getting icon...");
            _defaultIcon.SetActive(useDefault);
            if (useDefault) {
                _loadObject.SetActive(false);
                Debug.Log("Use default!");
                return;
            }
            _loadObject.SetActive(true);
            _go = Instantiate(_spritebase, _parent);
            await _go.GetComponent<IpfsConstructor>().ConstructIpfsObject(hash);
            if (this == null) {
                Debug.Log("This is null?");
                return;
            }
            _loadObject.SetActive(false);
            Debug.Log("Icon loaded?");
        }
        public void ClearIcon() {
            _loadObject.SetActive(false);
            if(_go != null)
                Destroy(_go);
        }
    }
}