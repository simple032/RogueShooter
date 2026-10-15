using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Vision;
namespace RogueShooter.Art
{
    /// <summary>Charge feedback belongs to the mouse reticle. No legacy body overlay.</summary>
    public class ChargeFxView : MonoBehaviour
    {
        ChargeReticle _reticle;
        float _progress;
        bool _charging;
        public void SetChargeProgress(float progress01, bool greenWindow)
        {
            EnsureReticle(); _charging = true; _progress = Mathf.Clamp01(progress01);
            _reticle.SetCoreVisible(true); _reticle.SetRingVisible(true); _reticle.SetProgress(_progress, greenWindow);
        }
        void Awake() { EnsureReticle(); HideAll(); }
        void OnEnable()
        {
            Cursor.visible=false;
            ChargeFxHooks.OnChargeEnterGreen+=OnGreen;
            ChargeFxHooks.OnChargeExitGreen+=OnExitGreen;
            ChargeFxHooks.OnCritConfirm+=HideAll;
        }
        void OnDisable()
        {
            Cursor.visible=true;
            ChargeFxHooks.OnChargeEnterGreen-=OnGreen;
            ChargeFxHooks.OnChargeExitGreen-=OnExitGreen;
            ChargeFxHooks.OnCritConfirm-=HideAll;
            if (_reticle != null) _reticle.gameObject.SetActive(false);
        }
        void OnDestroy() { if (_reticle != null) Destroy(_reticle.gameObject); }
        void LateUpdate()
        {
            // Mouse-driven overlays (reward cards, ESC pause menu): show the OS
            // cursor while they are open, keep it hidden for reticle-only aiming.
            Cursor.visible = RunPause.InteractOpen || RunPause.MenuOpen;
            FollowMouse();
        }
        void OnGreen() { if (_charging) SetChargeProgress(_progress,true); }
        void OnExitGreen() { if (_charging) SetChargeProgress(_progress,false); }
        public void HideAll()
        {
            _charging=false; _progress=0f;
            if (_reticle == null) return;
            _reticle.SetRingVisible(false); _reticle.SetProgress(0f,false); _reticle.SetCoreVisible(true);
        }
        void EnsureReticle()
        {
            if (_reticle != null) return;
            _reticle=new GameObject("ChargeReticle").AddComponent<ChargeReticle>(); FollowMouse();
        }
        void FollowMouse()
        {
            if (_reticle == null || Camera.main == null) return;
            _reticle.gameObject.SetActive(isActiveAndEnabled);
            var p=CameraViewMath.ScreenToWorldOnPlayPlane(Camera.main,Input.mousePosition);
            p.z=-.05f; _reticle.transform.position=p;
        }
    }
}
