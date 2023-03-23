using System.Collections;
using System.Collections.Generic;
using static Models;
using UnityEngine;

public class scr_WeaponController : MonoBehaviour {
    private scr_CharacterController characterController;

    [Header("Referenses")]
    public Animator weaponAnimator;

    [Header("Settings")]
    public WeaponSettingsModel settings;
    bool isInitialised;

    Vector3 newWeaponRotation;
    Vector3 newWeaponRotationVelocity;

    Vector3 targetWeaponRotation;
    Vector3 targetWeaponRotationVelocity;

    Vector3 newWeaponMovementRotation;
    Vector3 newWeaponMovementRotationVelocity;

    Vector3 targetWeaponMovementRotation;
    Vector3 targetWeaponMovementRotationVelocity;

    private bool isGroundedTrigger;

    public float fallingDelay;

    private void Start() {
        newWeaponRotation = transform.localRotation.eulerAngles;
    }

    public void Initialise(scr_CharacterController CharacterController) {
        characterController = CharacterController;
        isInitialised = true;
    }
    private void Update() {
        if (!isInitialised) {
            return;
        }

        CalculateWeaponRotation();
        SetWeaponAnimations();
    }

    public void TriggerJump() {
        isGroundedTrigger = false;
        weaponAnimator.SetTrigger("Jump");
    }

    private void CalculateWeaponRotation() {
        targetWeaponRotation.y += settings.SwayAmount * (settings.SwayXInverted ? -characterController.inputView.x : characterController.inputView.x) * Time.deltaTime;
        targetWeaponRotation.x += settings.SwayAmount * (settings.SwayYInverted ? characterController.inputView.y : -characterController.inputView.y) * Time.deltaTime;

        targetWeaponRotation.x = Mathf.Clamp(targetWeaponRotation.x, -settings.SwayClampX, settings.SwayClampX);
        targetWeaponRotation.y = Mathf.Clamp(targetWeaponRotation.y, -settings.SwayClampY, settings.SwayClampY);
        targetWeaponRotation.z = targetWeaponRotation.y;

        targetWeaponRotation = Vector3.SmoothDamp(targetWeaponRotation, Vector3.zero, ref targetWeaponRotationVelocity, settings.SwayResetSmoothing);
        newWeaponRotation = Vector3.SmoothDamp(newWeaponRotation, targetWeaponRotation, ref newWeaponRotationVelocity, settings.SwaySmoothing);

        targetWeaponMovementRotation.z = settings.MovementSwayX * (settings.MovementSwayXInverted ? -characterController.inputMovement.x : characterController.inputMovement.x);
        targetWeaponMovementRotation.x = settings.MovementSwayY * (settings.MovementSwayYInverted ? -characterController.inputMovement.y : characterController.inputMovement.y);

        targetWeaponMovementRotation = Vector3.SmoothDamp(targetWeaponMovementRotation, Vector3.zero, ref targetWeaponMovementRotationVelocity, settings.MovementSwaySmoothing);
        newWeaponMovementRotation = Vector3.SmoothDamp(newWeaponMovementRotation, targetWeaponMovementRotation, ref newWeaponMovementRotationVelocity, settings.MovementSwaySmoothing);

        transform.localRotation = Quaternion.Euler(newWeaponRotation + newWeaponMovementRotation);
    }

    private void SetWeaponAnimations() {
        if (isGroundedTrigger) {
            fallingDelay = 0f;
        } else {
            fallingDelay += Time.deltaTime;
        }

        if (characterController.isGrounded && !isGroundedTrigger && fallingDelay > 0.1f) {
            weaponAnimator.SetTrigger("Land");
            isGroundedTrigger = true;
        } else if (!characterController.isGrounded && isGroundedTrigger) {
            Debug.Log("Trigger Falling");
            weaponAnimator.SetTrigger("Falling");
            isGroundedTrigger = false;
        }
        weaponAnimator.SetBool("IsSprinting", characterController.isSprinting);
        weaponAnimator.SetFloat("WeaponAnimationSpeed", characterController.weaponAnimationSpeed);
    }
}
