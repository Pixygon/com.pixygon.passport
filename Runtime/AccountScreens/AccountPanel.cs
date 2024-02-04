using UnityEngine;

namespace Pixygon.Passport {
    public class AccountPanel : MonoBehaviour {
        [SerializeField] protected AccountUI _accountUi;
        protected virtual void ClearInputs() {
            
        }
        public virtual void ActivateScreen(bool active) {
            gameObject.SetActive(active);
            if(active) ClearInputs();
        }
    }
}