﻿using System;
using UnityEngine;
using Camera2.HarmonyPatches;
using Camera2.Interfaces;
using Camera2.Utils;
using static Camera2.Configuration.MovementScript;
using Camera2.Configuration;

namespace Camera2.Configuration {
	class Settings_MovementScript {
		public string[] scriptList = new string[] { };
	}
}

namespace Camera2.Middlewares {
	class MovementScriptProcessor : CamMiddleware, IMHandler {
		static System.Random randomSource = new System.Random();

		Transform scriptTransform = new GameObject("MovementScriptApplier").transform;

		MovementScript loadedScript = null;
		float currentAnimationTime = 0f;


		int frameIndex = 0;

		float lastFov = 0f;
		Vector3 lastPos = Vector3.zero;
		Quaternion lastRot = Quaternion.identity;

		Frame targetFrame { get { return loadedScript.frames[frameIndex]; } }

		private bool isParented = false;
		private void DoParent() {
			if(isParented)
				return;

			scriptTransform.parent = cam.UCamera.transform.parent;

			var pos = cam.UCamera.transform.localPosition;
			var rot = cam.UCamera.transform.localRotation;

			scriptTransform.localPosition = pos;
			scriptTransform.localRotation = rot;

			cam.UCamera.transform.parent = scriptTransform;
			isParented = true;
		}

		private void Reset() {
			if(loadedScript == null)
				return;

			scriptTransform.localPosition = lastPos = Vector3.zero;
			scriptTransform.localRotation = lastRot = Quaternion.identity;
			loadedScript = null;
			currentAnimationTime = 0f;
			frameIndex = 0;
			lastFov = 0f;
		}

		new public void CamConfigReloaded() {
			if(loadedScript == null)
				return;
			// Having a custom position on a camera thats executing a movement script is PROBABLY not what the user wants
			cam.transform.localPosition = Vector3.zero;
			cam.transform.localRotation = Quaternion.identity;
		}

		new public bool Pre() {
			if(settings.MovementScript.scriptList.Length == 0 || !SceneUtil.isInSong || cam.settings.type != Configuration.CameraType.Positionable) {
				Reset();
				return true;
			}

			if(loadedScript == null) {
				var scriptName = settings.MovementScript.scriptList[randomSource.Next(settings.MovementScript.scriptList.Length)];
				loadedScript = new MovementScript().LoadScript(scriptName);

				if(loadedScript == null || loadedScript.frames.Count == 1)
					return true;

				lastFov = cam.UCamera.fieldOfView;
				CamConfigReloaded();

				Plugin.Log.Info($"Applying Movementscript {scriptName} for camera {cam.name}");
				DoParent();
			}

			if(loadedScript.syncToSong) {
				currentAnimationTime = SceneUtil.audioTimeSyncController.songTime;
			} else {
				currentAnimationTime += cam.timeSinceLastRender;
			}

			if(currentAnimationTime > loadedScript.scriptDuration) {
				currentAnimationTime %= loadedScript.scriptDuration;
				frameIndex = 0;
			}

			for(;;) {
				if(targetFrame.endTime <= currentAnimationTime) {
					lastPos = scriptTransform.localPosition = targetFrame.position;
					lastRot = scriptTransform.localRotation = targetFrame.rotation;
					if(targetFrame.FOV > 0)
						lastFov = cam.UCamera.fieldOfView = targetFrame.FOV;
				} else if(targetFrame.startTime <= currentAnimationTime) {
					var frameProgress = (currentAnimationTime - targetFrame.startTime) / targetFrame.duration;

					// I wish this was possible in a more DRY code fashion
					if(targetFrame.transition == MoveType.Linear) {
						scriptTransform.localPosition = Vector3.Lerp(lastPos, targetFrame.position, frameProgress);
						scriptTransform.localRotation = Quaternion.Lerp(lastRot, targetFrame.rotation, frameProgress);

						if(targetFrame.FOV > 0f)
							cam.UCamera.fieldOfView = Mathf.Lerp(lastFov, targetFrame.FOV, frameProgress);
					} else {
						scriptTransform.localPosition = Vector3.Slerp(lastPos, targetFrame.position, frameProgress);
						scriptTransform.localRotation = Quaternion.Slerp(lastRot, targetFrame.rotation, frameProgress);

						if(targetFrame.FOV > 0f)
							cam.UCamera.fieldOfView = Mathf.SmoothStep(lastFov, targetFrame.FOV, frameProgress);
					}
					break;
				} else if(targetFrame.startTime > currentAnimationTime) {
					break;
				}

				if(frameIndex++ >= loadedScript.frames.Count)
					frameIndex = 0;
			}

			return true;
		}
	}
}