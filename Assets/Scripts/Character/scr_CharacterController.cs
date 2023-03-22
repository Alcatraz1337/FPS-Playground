using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Models;

public class scr_CharacterController : MonoBehaviour {
    private CharacterController characterController;
    private DefaultInput defaultInput;
    [HideInInspector]
    public Vector2 inputMovement;
    [HideInInspector]
    public Vector2 inputView;

    private Vector3 newCameraRotation;
    private Vector3 newCharacterRotation;

    [Header("References")]
    public Transform cameraHolder;
    public Transform feetTransform;

    [Header("Settings")]
    public PlayerSettingsModel playerSettings;
    public float viewClampYMin = -70;
    public float viewClampYMax = 80;
    public LayerMask playerMask;

    [Header("Gravity")]
    public float gravityAmount;
    public float gravityMin;
    private float playerGravity;
    public Vector3 jumpingForce;
    private Vector3 jumpingForceVelocity;

    [Header("Stance")]
    public PlayerStance playerStance;
    public float playerStanceSmoothing;
    public CharacterStance playerStandStance;
    public CharacterStance playerCrouchStance;
    public CharacterStance playerProneStance;

    private float stanceCheckErrorMargin = 0.05f;
    private float cameraHeight;
    private float cameraHeightVelocity;

    private Vector3 stanceCapsuleCenterVelocity;
    private float stanceCapsuleHeightVelocity;

    private bool isSprinting;

    private Vector3 newMovementSpeed;
    private Vector3 newMovementSpeedVelocity;

    [Header("Weapon")]
    public scr_WeaponController currentWeapon;

    public float weaponAnimationSpeed;

    private void Awake() {
        defaultInput = new DefaultInput();
        defaultInput.Character.Movement.performed += e => inputMovement = e.ReadValue<Vector2>();
        defaultInput.Character.View.performed += e => inputView = e.ReadValue<Vector2>();
        defaultInput.Character.Jump.performed += e => Jump();
        defaultInput.Character.Crouch.performed += e => Crouch();
        defaultInput.Character.Prone.performed += e => Prone();
        defaultInput.Character.Sprint.performed += e => ToggleSprint();
        defaultInput.Character.SprintReleased.performed += e => StopSprint();

        defaultInput.Enable();

        newCameraRotation = cameraHolder.localRotation.eulerAngles;
        newCharacterRotation = transform.localRotation.eulerAngles;

        characterController = GetComponent<CharacterController>();

        cameraHeight = cameraHolder.localPosition.y; // Think about why not position.y?

        if (currentWeapon) {
            currentWeapon.Initialise(this);
        }
    }

    private void Update() {
        CalculateMovement();
        CalculateView();
        CalculateJump();
        CalculateStance();
    }

    private void CalculateView() {
        newCharacterRotation.y += playerSettings.ViewXSensitivity * (playerSettings.ViewXInverted ? -inputView.x : inputView.x) * Time.deltaTime;
        transform.rotation = Quaternion.Euler(newCharacterRotation);

        newCameraRotation.x += playerSettings.ViewYSensitivity * (playerSettings.ViewYInverted ? inputView.y : -inputView.y) * Time.deltaTime;
        newCameraRotation.x = Mathf.Clamp(newCameraRotation.x, viewClampYMin, viewClampYMax);

        cameraHolder.localRotation = Quaternion.Euler(newCameraRotation);
    }

    private void CalculateMovement() {
        if (inputMovement.y <= 0.2f) {
            isSprinting = false;
        }

        var verticalSpeed = playerSettings.WalkingFowardSpeed;
        var horizontalSpeed = playerSettings.WalkingStrafeSpeed;

        if (isSprinting) {
            verticalSpeed = playerSettings.RunningForwardSpeed;
            horizontalSpeed = playerSettings.RunningStrafeSpeed;
        }

        if (!characterController.isGrounded) {
            playerSettings.SpeedEffector = playerSettings.FallingSpeedEffector;
        } else if (playerStance == PlayerStance.Crouch) {
            playerSettings.SpeedEffector = playerSettings.CrouchSpeedEffector;
        } else if (playerStance == PlayerStance.Prone) {
            playerSettings.SpeedEffector = playerSettings.ProneSpeedEffector;
        } else {
            playerSettings.SpeedEffector = 1;
        }

        weaponAnimationSpeed = characterController.velocity.magnitude / (playerSettings.WalkingFowardSpeed * playerSettings.SpeedEffector);

        if (weaponAnimationSpeed > 1)
            weaponAnimationSpeed = 1;

        verticalSpeed *= playerSettings.SpeedEffector;
        horizontalSpeed *= playerSettings.SpeedEffector;

        newMovementSpeed = Vector3.SmoothDamp(newMovementSpeed, new Vector3(horizontalSpeed * inputMovement.x * Time.deltaTime, 0, verticalSpeed * inputMovement.y * Time.deltaTime), ref newMovementSpeedVelocity, characterController.isGrounded ? playerSettings.MovementSmoothing : playerSettings.FallingSmoothing); // 根据姿态调整Damping
        var movementSpeed = transform.TransformDirection(newMovementSpeed);

        if (playerGravity > gravityMin) {
            playerGravity -= gravityAmount * Time.deltaTime;
        }

        if (playerGravity < -0.1f && characterController.isGrounded) {
            playerGravity = -0.1f;
        }

        movementSpeed.y += playerGravity;
        movementSpeed += jumpingForce * Time.deltaTime;

        characterController.Move(movementSpeed);
    }

    private void CalculateJump() {
        jumpingForce = Vector3.SmoothDamp(jumpingForce, Vector3.zero, ref jumpingForceVelocity, playerSettings.JumpingFalloff);
    }

    private void CalculateStance() {
        var currentStance = playerStandStance;

        if (playerStance == PlayerStance.Crouch) {
            currentStance = playerCrouchStance;
        } else if (playerStance == PlayerStance.Prone) {
            currentStance = playerProneStance;
        }
        cameraHeight = Mathf.SmoothDamp(cameraHolder.localPosition.y, currentStance.CameraHeight, ref cameraHeightVelocity, playerStanceSmoothing);
        cameraHolder.localPosition = new Vector3(cameraHolder.localPosition.x, cameraHeight, cameraHolder.localPosition.z);

        characterController.height = Mathf.SmoothDamp(characterController.height, currentStance.StanceCollider.height, ref stanceCapsuleHeightVelocity, playerStanceSmoothing);
        characterController.center = Vector3.SmoothDamp(characterController.center, currentStance.StanceCollider.center, ref stanceCapsuleCenterVelocity, playerStanceSmoothing);
    }

    private void Jump() {
        if (!characterController.isGrounded || StanceCheck(playerStandStance.StanceCollider.height))
            return;

        if (playerStance == PlayerStance.Crouch || playerStance == PlayerStance.Prone) {
            playerStance = PlayerStance.Stand;
            return;
        }

        // Jump
        jumpingForce = Vector3.up * playerSettings.JumpingHeight;
        playerGravity = 0;
    }

    private void Crouch() {
        if (playerStance == PlayerStance.Crouch) {
            if (StanceCheck(playerStandStance.StanceCollider.height)) {
                return;
            }
            playerStance = PlayerStance.Stand;
            return;
        }
        if (StanceCheck(playerCrouchStance.StanceCollider.height)) {
            return;
        }
        playerStance = PlayerStance.Crouch;
    }

    private void Prone() {

        playerStance = PlayerStance.Prone;

    }
    private bool StanceCheck(float stanceCheckHeight) {
        Vector3 start = new Vector3(feetTransform.position.x, feetTransform.position.y + characterController.radius + stanceCheckErrorMargin, feetTransform.position.z); // 为什么需要Error Margin？
        Vector3 end = new Vector3(feetTransform.position.x, feetTransform.position.y - stanceCheckErrorMargin + stanceCheckHeight, feetTransform.position.z);

        return Physics.CheckCapsule(start, end, characterController.radius, playerMask);
    }

    private void ToggleSprint() {
        if (inputMovement.y <= 0.2f) {
            isSprinting = false;
            return;
        }

        isSprinting = !isSprinting;
    }
    private void StopSprint() {
        if (playerSettings.SprintingHold) {
            isSprinting = false;
        }
    }

}
