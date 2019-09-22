using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.GPUAnimation
{
	/// <summary>
	/// 存储动画片段的持续时间和编号
	/// </summary>
	public struct GPUAnimationState : IComponentData
	{
		public float Time;
		public int   AnimationClipIndex;
		
		public BlobAssetReference<BakedAnimationClipSet> AnimationClipSet;
	}
	
	struct AnimationTextureCoordinate : IComponentData
	{
		public float3 Coordinate;
	}
	
	
	public struct BakedAnimationClipSet
	{
		public BlobArray<BakedAnimationClip> Clips;
	}

	/// <summary>
	/// 存储Animation Clip数据在材质中的具体信息
	/// </summary>
	public struct BakedAnimationClip
	{
		/// <summary>
		/// start = (float)clipData.PixelStart / animTextures.Animation0.width
		/// </summary>
		internal float TextureOffset;

		/// <summary>
		/// end - start
		/// </summary>
		internal float TextureRange;

		/// <summary>
		/// 1f / animTextures.Animation0.width
		/// </summary>
		internal float OnePixelOffset;

		/// <summary>
		/// animTextures.Animation0.width
		/// Width of the texture in pixels. (Read Only)
		/// 相对应的有个height(Height of the texture in pixels. (Read Only))
		/// </summary>
		internal float TextureWidth;

		/// <summary>
		/// 1.0F / TextureWidth
		/// </summary>
		internal float OneOverTextureWidth;

		/// <summary>
		/// 1.0F / OnePixelOffset
		/// </summary>
		internal float OneOverPixelOffset;

		/// <summary>
		/// clipData.Clip.length
		/// </summary>
		public float AnimationLength;

		/// <summary>
		/// clipData.Clip.wrapMode == WrapMode.Loop
		/// </summary>
		public bool  Looping;

		public BakedAnimationClip(AnimationTextures animTextures, KeyframeTextureBaker.AnimationClipData clipData)
		{
			float onePixel = 1f / animTextures.Animation0.width;
			float start = (float)clipData.PixelStart / animTextures.Animation0.width;
			float end = (float)clipData.PixelEnd / animTextures.Animation0.width;

			TextureOffset = start;
			TextureRange = end - start;
			OnePixelOffset = onePixel;
			TextureWidth = animTextures.Animation0.width;
			// QUESTION 为什么要倒数？
			OneOverTextureWidth = 1.0F / TextureWidth;
			OneOverPixelOffset = 1.0F / OnePixelOffset;
			
			AnimationLength = clipData.Clip.length;
			Looping = clipData.Clip.wrapMode == WrapMode.Loop;
		}
		
		public float3 ComputeCoordinate(float normalizedTime)
		{
			float texturePosition = normalizedTime * TextureRange + TextureOffset;
			float lowerPixelFloor = math.floor(texturePosition * TextureWidth);

			/// QUESTION：OneOverTextureWidth和OnePixelOffset难道不是同一个东西吗？
			/// TextureWidth = animTextures.Animation0.width;
			/// OneOverTextureWidth = 1.0F / TextureWidth;
			/// float onePixel = 1f / animTextures.Animation0.width;
			/// OnePixelOffset = onePixel;
			float lowerPixelCenter = lowerPixelFloor * OneOverTextureWidth;
			float upperPixelCenter = lowerPixelCenter + OnePixelOffset;
			/// QUESTION：TextureWidth和OneOverPixelOffset有什么区别？
			/// TextureWidth = animTextures.Animation0.width;
			/// float onePixel = 1f / animTextures.Animation0.width;
			///  OnePixelOffset = onePixel;
			///  OneOverPixelOffset = 1.0F / OnePixelOffset;
			float lerpFactor = (texturePosition - lowerPixelCenter) * OneOverPixelOffset;

			return  new float3(lowerPixelCenter, upperPixelCenter, lerpFactor);
		}
		
		/// <summary>
		/// 计算当前时间，获得一个比0大，比1小的百分比
		/// </summary>
		/// <param name="time"></param>
		/// <returns></returns>
		public float ComputeNormalizedTime(float time)
		{
			if (Looping)
				return Mathf.Repeat(time, AnimationLength) / AnimationLength;
			else
				// Returns the result of clamping the value x into the interval [a, b], where x, a and b are float values.
				// public static float saturate(float x) { return clamp(x, 0.0f, 1.0f); }

				/// <summary>Returns the result of clamping the value x into the interval [a, b], where x, a and b are float values.</summary>
				/// public static float clamp(float x, float a, float b) { return max(a, min(b, x)); }
				return math.saturate(time / AnimationLength);
		}

	}

	/// <summary>
	/// 准备好Material，Animation Texture、Mesh之后就可以准备绘制
	/// </summary>
	[System.Serializable]
	struct RenderCharacter : ISharedComponentData, IEquatable<RenderCharacter>
	{
		//@TODO: Would be nice if we had BlobAssetReference in shared component data support (Serialize not supported...) 
		public Material                                  Material;
		public AnimationTextures                         AnimationTexture;
		public Mesh                                      Mesh;
		public bool                                      ReceiveShadows;
		public ShadowCastingMode                         CastShadows;
		
		public bool Equals(RenderCharacter other)
		{
			return Material == other.Material && AnimationTexture.Equals(other.AnimationTexture) && Mesh == other.Mesh && ReceiveShadows == other.ReceiveShadows && CastShadows == other.CastShadows;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (ReferenceEquals(Material, null) ? 0 : Material.GetHashCode());
				hashCode = (hashCode * 397) ^ AnimationTexture.GetHashCode();
				hashCode = (hashCode * 397) ^ (ReferenceEquals(Mesh, null) ? 0 : Mesh.GetHashCode());
				return hashCode;
			}
		}
	}

	unsafe public static class NativeExtensionTemp
	{
		/// <summary>
		/// 扩展函数
		///  Reinterpret：重新解释
		///  NativeArray<T>转换成 NativeArray<U>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="U"></typeparam>
		/// <param name="array"></param>
		/// <returns></returns>
		public static NativeArray<U> Reinterpret_Temp<T, U>(this NativeArray<T> array) where U : struct where T : struct
        {
            var tSize = UnsafeUtility.SizeOf<T>();
            var uSize = UnsafeUtility.SizeOf<U>();

             var byteLen = ((long) array.Length) * tSize;
            var uLen = byteLen / uSize;

 #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (uLen * uSize != byteLen)
            {
                throw new InvalidOperationException($"Types {typeof(T)} (array length {array.Length}) and {typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            }

 #endif
            var ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(ptr, (int) uLen, Allocator.Invalid);

 #if ENABLE_UNITY_COLLECTIONS_CHECKS
            var handle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, handle);
#endif

             return result;
        }

	}

	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public class CalculateTextureCoordinateSystem : JobComponentSystem
	{
		[BurstCompile]
		struct CalculateTextureCoordJob : IJobForEach<GPUAnimationState, AnimationTextureCoordinate>
		{
			public void Execute([ReadOnly]ref GPUAnimationState animstate, ref AnimationTextureCoordinate textureCoordinate)
			{
				ref var clips = ref animstate.AnimationClipSet.Value.Clips;
				if ((uint) animstate.AnimationClipIndex < (uint) clips.Length)
				{
					var normalizedTime = clips[animstate.AnimationClipIndex].ComputeNormalizedTime(animstate.Time);
					textureCoordinate.Coordinate = clips[animstate.AnimationClipIndex].ComputeCoordinate(normalizedTime);
				}
				else
				{
					// How to warn???
				}
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new CalculateTextureCoordJob().Schedule(this, inputDeps);
		}
	}

	[UpdateInGroup(typeof(PresentationSystemGroup))]
	[UpdateAfter(typeof(CalculateTextureCoordinateSystem))]
	public class GpuCharacterRenderSystem : JobComponentSystem
    {
		// 创建绘制的角色列表
	    private List<RenderCharacter> _Characters = new List<RenderCharacter>();
	    private Dictionary<RenderCharacter, InstancedSkinningDrawer> _Drawers = new Dictionary<RenderCharacter, InstancedSkinningDrawer>();

	    private EntityQuery m_Characters;


	    protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
	        _Characters.Clear();
	        EntityManager.GetAllUniqueSharedComponentData(_Characters);

	        foreach (var character in _Characters)
	        {
		        if (character.Material == null || character.Mesh == null)
			        continue;
		        
		        //@TODO: Currently we never cleanup the _Drawers cache when the last entity with that renderer disappears.
		        InstancedSkinningDrawer drawer;
		        if (!_Drawers.TryGetValue(character, out drawer))
		        {
					// 对要绘制的角色实例化一个Drawer
			        drawer = new InstancedSkinningDrawer(character.Material, character.Mesh, character.AnimationTexture);
			        _Drawers.Add(character, drawer);
		        }
		        
				m_Characters.SetFilter(character);

				Profiler.BeginSample("ExtractState");
				JobHandle jobA, jobB;
				// 传输坐标和LocalToWorld矩阵
		        var coords = m_Characters.ToComponentDataArray<AnimationTextureCoordinate>(Allocator.TempJob, out jobA);
		        var localToWorld = m_Characters.ToComponentDataArray<LocalToWorld>(Allocator.TempJob, out jobB);
		        JobHandle.CompleteAll(ref jobA, ref jobB);
		        Profiler.EndSample();
		        
				// 调用Draw()方法
		        drawer.Draw(coords.Reinterpret_Temp<AnimationTextureCoordinate, float3>(), localToWorld.Reinterpret_Temp<LocalToWorld, float4x4>(), character.CastShadows, character.ReceiveShadows);
		        
		        coords.Dispose();
		        localToWorld.Dispose();
	        }

	        return inputDeps;
        }

        protected override void OnCreate()
        {
	        m_Characters = GetEntityQuery(ComponentType.ReadOnly<RenderCharacter>(), ComponentType.ReadOnly<GPUAnimationState>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<AnimationTextureCoordinate>());
        }

        protected override void OnDestroy()
        {
	        foreach(var drawer in _Drawers.Values)
		        drawer.Dispose();
	        _Drawers = null;
        }
    }
}