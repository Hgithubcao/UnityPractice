﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = System.Object;


namespace Unity.GPUAnimation
{
	/// <summary>
	/// 存储转换成Texture2D数据的动画片段
	/// 每个动画片段会在定点处进行三次采样
	/// </summary>
	public struct AnimationTextures : IEquatable<AnimationTextures>
	{
		public Texture2D Animation0;
		public Texture2D Animation1;
		public Texture2D Animation2;

		public bool Equals(AnimationTextures other)
		{
			return Animation0 == other.Animation0 && Animation1 == other.Animation1 && Animation2 == other.Animation2;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (ReferenceEquals(Animation0, null) ? 0 : Animation0.GetHashCode());
				hashCode = (hashCode * 397) ^ (ReferenceEquals(Animation1, null) ? 0 : Animation1.GetHashCode());
				hashCode = (hashCode * 397) ^ (ReferenceEquals(Animation2, null) ? 0 : Animation2.GetHashCode());
				return hashCode;
			}
		}
	}

	public static class KeyframeTextureBaker
	{
		

		/// <summary>
		/// 存储转换成Texture2D变量后的动画片段数据和Mesh、LOD、帧率等
		/// </summary>
		public class BakedData
		{
			public AnimationTextures AnimationTextures;
			public Mesh NewMesh;
			public LodData lods;
			public float Framerate;

			public List<AnimationClipData> Animations = new List<AnimationClipData>();

			public Dictionary<string, AnimationClipData> AnimationsDictionary = new Dictionary<string, AnimationClipData>();
		}

		/// <summary>
		/// 存储原始的动画片段和该动画片段在Texture2D中对应起始和终止像素
		/// </summary>
		public class AnimationClipData
		{
			public AnimationClip Clip;
			public int PixelStart;
			public int PixelEnd;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="animationRoot">动画根对象</param>
		/// <param name="animationClips">动画片段数组</param>
		/// <param name="framerate">帧率</param>
		/// <param name="lods">LOD数据</param>
		/// <returns></returns>
		public static BakedData BakeClips(GameObject animationRoot, AnimationClip[] animationClips, float framerate, LodData lods)
		{
			//首先获取动画根对象子对象的SkinMeshRenderer
			var skinRenderers = animationRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
			if (skinRenderers.Length != 1)
				throw new System.ArgumentException("There must be exactly one SkinnedMeshRenderer");

			// @TODO: warning about more than one materials

			// messing 是不整洁的   arbitrary 任意的
			// Before messing about with some arbitrary game object hierarchy.
			// Instantiate the character, but make sure it's inactive so it doesn't trigger any unexpected systems. 
			var wasActive = animationRoot.activeSelf;
			animationRoot.SetActive(false);
			var instance = GameObject.Instantiate(animationRoot, Vector3.zero, Quaternion.identity);
			animationRoot.SetActive(wasActive);
			
			instance.transform.localScale = Vector3.one;
			var skinRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();

			BakedData bakedData = new BakedData();
			bakedData.NewMesh = CreateMesh(skinRenderer);               // 利用这个skinRenderer作为CreateMesh方法的参数，生成BakedData的NewMesh
			// BakedData的LodData结构体中的mesh成员也使用CreateMesh生成，只不过需要的第二个参数是输入lod的mesh成员
			var lod1Mesh = CreateMesh(skinRenderer, lods.Lod1Mesh);	
			var lod2Mesh = CreateMesh(skinRenderer, lods.Lod2Mesh);
			var lod3Mesh = CreateMesh(skinRenderer, lods.Lod3Mesh);
			bakedData.lods = new LodData(lod1Mesh, lod2Mesh, lod3Mesh, lods.Lod1Distance, lods.Lod2Distance, lods.Lod3Distance);

			bakedData.Framerate = framerate;    // BakedData的Framerate直接使用输入的framerate

			var sampledBoneMatrices = new List<Matrix4x4[,]>();

			int numberOfKeyFrames = 0;

			for (int i = 0; i < animationClips.Length; i++)
			{
				// 使用SampleAnimationClip方法对每个动画片段采样得到sapledMatrix，然后添加到list中
				var sampledMatrix = SampleAnimationClip(instance, animationClips[i], skinRenderer, bakedData.Framerate);
				sampledBoneMatrices.Add(sampledMatrix);

				// 使用sampledBoneMatrices的维数参数作为关键帧和骨骼的数目统计
				numberOfKeyFrames += sampledMatrix.GetLength(0);
			}

			int numberOfBones = sampledBoneMatrices[0].GetLength(1);

			// 使用骨骼数和关键帧数作为大小创建材质
			var tex0 = bakedData.AnimationTextures.Animation0 = new Texture2D(numberOfKeyFrames, numberOfBones, TextureFormat.RGBAFloat, false);
			tex0.wrapMode = TextureWrapMode.Clamp;
			tex0.filterMode = FilterMode.Point;
			tex0.anisoLevel = 0;

			var tex1 = bakedData.AnimationTextures.Animation1 = new Texture2D(numberOfKeyFrames, numberOfBones, TextureFormat.RGBAFloat, false);
			tex1.wrapMode = TextureWrapMode.Clamp;
			tex1.filterMode = FilterMode.Point;
			tex1.anisoLevel = 0;

			var tex2 = bakedData.AnimationTextures.Animation2 = new Texture2D(numberOfKeyFrames, numberOfBones, TextureFormat.RGBAFloat, false);
			tex2.wrapMode = TextureWrapMode.Clamp;
			tex2.filterMode = FilterMode.Point;
			tex2.anisoLevel = 0;

			Color[] texture0Color = new Color[tex0.width * tex0.height];
			Color[] texture1Color = new Color[tex0.width * tex0.height];
			Color[] texture2Color = new Color[tex0.width * tex0.height];

			int runningTotalNumberOfKeyframes = 0;
			for (int i = 0; i < sampledBoneMatrices.Count; i++)
			{
				for (int boneIndex = 0; boneIndex < sampledBoneMatrices[i].GetLength(1); boneIndex++)
				{
					//Color previousRotation = new Color();

					for (int keyframeIndex = 0; keyframeIndex < sampledBoneMatrices[i].GetLength(0); keyframeIndex++)
					{
						//var rotation = GetRotation(Quaternion.LookRotation(sampledBoneMatrices[i][keyframeIndex, boneIndex].GetColumn(2),
						//													sampledBoneMatrices[i][keyframeIndex, boneIndex].GetColumn(1)));

						//if (keyframeIndex != 0)
						//{
						//	if (Distance(previousRotation, rotation) > Distance(Negate(rotation), previousRotation))
						//	{
						//		rotation = new Color(-rotation.r, -rotation.g, -rotation.b, -rotation.a);
						//	}
						//}

						//var translation = GetTranslation(sampledBoneMatrices[i][keyframeIndex, boneIndex].GetColumn(3), rotation);

						//previousRotation = rotation;
						//int index = Get1DCoord(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex, bakedData.TranslationTexture.width);
						//translations[index] = translation;
						//rotations[index] = rotation;

						int index = Get1DCoord(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex, tex0.width);

						// 将sampledBoneMatrices的数据全部存入到材质颜色中
						texture0Color[index] = sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(0);
						texture1Color[index] = sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(1);
						texture2Color[index] = sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(2);
					}
				}

				AnimationClipData clipData = new AnimationClipData
				{
					Clip = animationClips[i],
					// 生成AnimationClipData需要的开始结束位置
					PixelStart = runningTotalNumberOfKeyframes + 1,
					PixelEnd = runningTotalNumberOfKeyframes + sampledBoneMatrices[i].GetLength(0) - 1
				};

				if (clipData.Clip.wrapMode == WrapMode.Default) clipData.PixelEnd -= 1;

				bakedData.Animations.Add(clipData);

				runningTotalNumberOfKeyframes += sampledBoneMatrices[i].GetLength(0);
			}

			tex0.SetPixels(texture0Color);
			tex1.SetPixels(texture1Color);
			tex2.SetPixels(texture2Color);

			runningTotalNumberOfKeyframes = 0;
			for (int i = 0; i < sampledBoneMatrices.Count; i++)
			{
				for (int boneIndex = 0; boneIndex < sampledBoneMatrices[i].GetLength(1); boneIndex++)
				{
					for (int keyframeIndex = 0; keyframeIndex < sampledBoneMatrices[i].GetLength(0); keyframeIndex++)
					{
						//int d1_index = Get1DCoord(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex, bakedData.Texture0.width);

						Color pixel0 = tex0.GetPixel(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex);
						Color pixel1 = tex1.GetPixel(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex);
						Color pixel2 = tex2.GetPixel(runningTotalNumberOfKeyframes + keyframeIndex, boneIndex);

						if ((Vector4)pixel0 != sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(0))
						{
							Debug.LogError("Error at (" + (runningTotalNumberOfKeyframes + keyframeIndex) + ", " + boneIndex + ") expected " + Format(sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(0)) + " but got " + Format(pixel0));
						}
						if ((Vector4)pixel1 != sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(1))
						{
							Debug.LogError("Error at (" + (runningTotalNumberOfKeyframes + keyframeIndex) + ", " + boneIndex + ") expected " + Format(sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(1)) + " but got " + Format(pixel1));
						}
						if ((Vector4)pixel2 != sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(2))
						{
							Debug.LogError("Error at (" + (runningTotalNumberOfKeyframes + keyframeIndex) + ", " + boneIndex + ") expected " +   Format(sampledBoneMatrices[i][keyframeIndex, boneIndex].GetRow(2)) + " but got " + Format(pixel2));
						}
					}
				}
				runningTotalNumberOfKeyframes += sampledBoneMatrices[i].GetLength(0);
			}

			tex0.Apply(false, true);
			tex1.Apply(false, true);
			tex2.Apply(false, true);
			
			// 创建Dictionary字段
			bakedData.AnimationsDictionary = new Dictionary<string, AnimationClipData>();
			foreach (var clipData in bakedData.Animations)
			{
				bakedData.AnimationsDictionary[clipData.Clip.name] = clipData;
			}
			
			GameObject.DestroyImmediate(instance);

			return bakedData;
		}

		public static string Format(Vector4 v)
		{
			return "(" + v.x + ", " + v.y + ", " + v.z + ", " + v.w + ")";
		}

		public static string Format(Color v)
		{
			return "(" + v.r + ", " + v.g + ", " + v.b + ", " + v.a + ")";
		}

		/// <summary>
		/// 从已有的SkinnedMeshRenderer和一个Mesh创建一个新的Mesh。
		/// </summary>
		/// <param name="originalRenderer"></param>
		/// <param name="mesh">如果为空，则生成新的newMesh是原来Renderer的sharedMesh的复制</param>
		/// <returns></returns>
		private static Mesh CreateMesh(SkinnedMeshRenderer originalRenderer, Mesh mesh = null)
		{
			Mesh newMesh = new Mesh();
			Mesh originalMesh = mesh == null ? originalRenderer.sharedMesh : mesh;
			var boneWeights = originalMesh.boneWeights;

			originalMesh.CopyMeshData(newMesh);

			Vector3[] vertices = originalMesh.vertices;
			Vector2[] boneIds = new Vector2[originalMesh.vertexCount];
			Vector2[] boneInfluences = new Vector2[originalMesh.vertexCount];

			int[] boneRemapping = null;

			// 如果mesh非空，找到Mesh在sharedMesh中对应的bindPoses，把boneIndex0和bineIndex1映射到给定的Mesh上
			if (mesh != null)
			{
				var originalBindPoseMatrices = originalRenderer.sharedMesh.bindposes;
				var newBindPoseMatrices = mesh.bindposes;
				
				if (newBindPoseMatrices.Length != originalBindPoseMatrices.Length)
				{
					//Debug.LogError(mesh.name + " - Invalid bind poses, got " + newBindPoseMatrices.Length + ", but expected "
					//				+ originalBindPoseMatrices.Length);
				}
				else
				{
					boneRemapping = new int[originalBindPoseMatrices.Length];
					for (int i = 0; i < boneRemapping.Length; i++)
					{
						boneRemapping[i] = Array.FindIndex(originalBindPoseMatrices, x => x == newBindPoseMatrices[i]);
					}
				}
			}
			
			var bones = originalRenderer.bones;
			for (int i = 0; i < originalMesh.vertexCount; i++)
			{
				int boneIndex0 = boneWeights[i].boneIndex0;
				int boneIndex1 = boneWeights[i].boneIndex1;

				if (boneRemapping != null)
				{
					boneIndex0 = boneRemapping[boneIndex0];
					boneIndex1 = boneRemapping[boneIndex1];
				}

				// 通过boneWights的boneIndex0和boneIndex1生成的boneInfluences,作为newMesh的UV2和UV3存储起来
				boneIds[i] = new Vector2((boneIndex0 + 0.5f) / bones.Length, (boneIndex1 + 0.5f) / bones.Length);

				float mostInfluentialBonesWeight = boneWeights[i].weight0 + boneWeights[i].weight1;

				boneInfluences[i] = new Vector2(boneWeights[i].weight0 / mostInfluentialBonesWeight, boneWeights[i].weight1 / mostInfluentialBonesWeight);
			}

			newMesh.vertices = vertices;
			newMesh.uv2 = boneIds;
			newMesh.uv3 = boneInfluences;

			return newMesh;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="root">动画对象</param>
		/// <param name="clip">单个动画片段</param>
		/// <param name="renderer">SkinnedMeshRenderer</param>
		/// <param name="framerate">帧率</param>
		/// <returns>动画片段采样后生成的Matrices</returns>
		private static Matrix4x4[,] SampleAnimationClip(GameObject root, AnimationClip clip, SkinnedMeshRenderer renderer, float framerate)
		{
			var bindPoses = renderer.sharedMesh.bindposes;
			var bones = renderer.bones;
			Matrix4x4[,] boneMatrices = new Matrix4x4[Mathf.CeilToInt(framerate * clip.length) + 3, bones.Length];
			for (int i = 1; i < boneMatrices.GetLength(0) - 1; i++)
			{
				// 选取当前所在帧的clip数据作为一段时间的采样
				float t = (float)(i - 1) / (boneMatrices.GetLength(0) - 3);

				var oldWrapMode = clip.wrapMode;
				clip.wrapMode = WrapMode.Clamp;
				clip.SampleAnimation(root, t * clip.length);
				clip.wrapMode = oldWrapMode;
				
				for (int j = 0; j < bones.Length; j++)
					boneMatrices[i, j] = bones[j].localToWorldMatrix * bindPoses[j];
			}

			for (int j = 0; j < bones.Length; j++)
			{
				boneMatrices[0, j] = boneMatrices[boneMatrices.GetLength(0) - 2, j];
				boneMatrices[boneMatrices.GetLength(0) - 1, j] = boneMatrices[1, j];
			}

			return boneMatrices;
		}

		#region Util methods

		public static void CopyMeshData(this Mesh originalMesh, Mesh newMesh)
		{
			var vertices = originalMesh.vertices;

			newMesh.vertices = vertices;
			newMesh.triangles = originalMesh.triangles;
			newMesh.normals = originalMesh.normals;
			newMesh.uv = originalMesh.uv;
			newMesh.tangents = originalMesh.tangents;
			newMesh.name = originalMesh.name;
		}

		private static float Distance(Color r1, Color r2)
		{
			return Mathf.Abs(r1.r - r2.r) + Mathf.Abs(r1.g - r2.g) + Mathf.Abs(r1.b - r2.b) + Mathf.Abs(r1.a - r2.a);
		}

		private static Color Negate(Color c)
		{
			return new Color(-c.r, -c.g, -c.b, -c.a);
		}

		private static Color GetTranslation(Vector4 rawTranslation, Color rotation)
		{
			Quaternion rot = new Quaternion(rotation.r, rotation.g, rotation.b, rotation.a);
			Quaternion trans = new Quaternion(rawTranslation.x, rawTranslation.y, rawTranslation.z, 0) * rot;

			return new Color(trans.x, trans.y, trans.z, trans.w) * 0.5f;
		}

		private static Color GetRotation(Quaternion rotation)
		{
			return new Color(rotation.x, rotation.y, rotation.z, rotation.w);
		}

		private static int Get1DCoord(int x, int y, int width)
		{
			return y * width + x;
		}

		#endregion
}
}