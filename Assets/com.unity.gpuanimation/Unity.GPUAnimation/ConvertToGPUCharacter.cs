using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.GPUAnimation
{
	public static class CharacterUtility
	{
		/// <summary>
		/// Blob		
		/// n. 	(��ָҺ���) һ�㣬һ��; (��ɫ��) һСƬ���ߵ�;
		/// vt.Ū��; Ū��
		/// https://gametorrahod.com/everything-about-isharedcomponentdata/
		/// source-chamber distance(����)
		/// chamber				n. 	������; (����) ��Ժ; (���ض���;��) ���䣬��
		/// tightly				adv. 	������; �ι̵�; ���ܵ�
		/// restriction			n. 	���ƹ涨; ���Ʒ���; ����; Լ��; ��Լ����
		/// portal				n. 	׳�۵Ĵ���; ���������; �Ż���վ; ���վ��
		/// Aliasing			n. 	����ʹ�ã���������
		/// via					prep. 	���ɣ�����(ĳһ�ط�); ͨ����ƾ��(ĳ�ˡ�ϵͳ��
		/// sneak				v. 	͵͵����; ��; ͵͵����; ͵��; ͵��; ͵��(����Ҫ�Ļ�С�Ķ���)
		/// concept				n. 	����; ����
		/// optimizable			 ���Ż���
		/// hell				n. 	����; ���ѵľ���; ���ҵľ���; (������Ϊ��ð����) ����������
		/// kinda				���ڱ����У���ʾ����ʽ�Ự�� kind of �ķ���;
		/// lawless				adj. 	�޷��ɵ�; �����ط��ɵ�; Ŀ�޷��͵�; ������
		/// compatible			adj. 	�ɹ��õ�; ���ݵ�; �ɹ����; (��־Ȥ����Ͷ��) ��ϵ�õģ������ദ��
		/// interact with		v. 	�롭�໥����
		/// grabbed				v. 	ץס; ��ȡ; (��ͼ) ץס�����; ���ã�ץס(����)
		/// pierce				v. 	��; ����; ��͸; ����; ͸��; ����; ͻ��
		/// hassle				n. 	����; �鷳; ����; ����; ����
		/// streamline			v. 	ʹ��������; ʹ(ϵͳ��������)Ч�ʸ���; (��ָ) ʹ������Լ
		/// drill down to		���뵽 
		/// recursively			�ݹ��; �ݹ�; �ݹ��; �ݹ�ɾ��; �ع�
		/// removal				n. 	�ƶ�; ����; ȥ��; ��ȥ; ����; ���; ��ְ; ��ְ
		/// maintaining			v. 	ά��; ����; ά��; ����; ���(���); ��ִ����
		/// chronological		adj. 	������ʱ��˳�����е�; ��ʱ������(����)(��������塢��������еȷ���ķ�չ����); 
		/// adjacent			adj. 	�롭������; �ڽ���
		/// galore				adj. 	����; �ܶ�
		/// dare to				v. 	����
		/// segmenting			v. 	�ָ�; ����
		/// segmenting via		�ֶ�ͨ�� 
		/// extensively			adv. 	���أ��㷺��
		/// fancy				adj. 	�쳣���ӵ�; ̫���ڵ�; ���µ�; �о���װ�ε�; Ѥ����; ���ڵ�; �����; �ݻ���
		/// categorizing		v. 	��������; �ѡ����Թ���
		/// intending			v.����; �ƻ�; ��Ҫ; ��ָ
		/// Quoted				v. 	����; ����; ����˵��; ����; ����; ����
		/// prior				adj. 	��ǰ��; �����; ��ǰ��; ���ȵ�; ռ�ȵ�; ����Ҫ��; ��ǰ���
		/// http://gametorrahod.com/designing-an-efficient-system-with-version-numbers/
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static BlobAssetReference<BakedAnimationClipSet> CreateClipSet(KeyframeTextureBaker.BakedData data)
		{
			using (var builder = new BlobBuilder(Allocator.Temp))
			{
				ref var root = ref builder.ConstructRoot<BakedAnimationClipSet>();
				var clips = builder.Allocate(data.Animations.Count, ref root.Clips);
				for (int i = 0; i != data.Animations.Count; i++)
					clips[i] = new BakedAnimationClip(data.AnimationTextures, data.Animations[i]);

				return builder.CreateBlobAssetReference<BakedAnimationClipSet>(Allocator.Persistent);
			}
		}

		/// <summary>
		/// �ѽ�ɫת���ɿ���ʹ��GPU��Ⱦ�Ĺؼ�
		/// </summary>
		/// <param name="manager"></param>
		/// <param name="entity"></param>
		/// <param name="characterRig"></param>
		/// <param name="clips"></param>
		/// <param name="framerate"></param>
		public static void AddCharacterComponents(EntityManager manager, Entity entity, GameObject characterRig, AnimationClip[] clips, float framerate)
		{
			// SkinnedMeshRenderer : the skinned mesh filter
			// SkinnedMeshRenderer.sharedMesh : the mesh used for skinning
			var renderer = characterRig.GetComponentInChildren<SkinnedMeshRenderer>();

			//Debug.Log(renderer.gameObject + "   " + renderer.sharedMesh);    "minion_skeleton"
			var lod = new LodData
			{
				Lod1Mesh = renderer.sharedMesh,
				Lod2Mesh = renderer.sharedMesh,
				Lod3Mesh = renderer.sharedMesh,
				Lod1Distance = 0,
				Lod2Distance = 100,
				Lod3Distance = 10000,
			};

			// validation ��Ч
			//@TODO: Perform validation that the shader supports GPU Skinning mode
			var bakedData = KeyframeTextureBaker.BakeClips(characterRig, clips, framerate, lod);

			// ����manager��entity���������animation state, texturecoordinate, rendercharacter
			var animState = default(GPUAnimationState);
			animState.AnimationClipSet = CreateClipSet(bakedData);
			manager.AddComponentData(entity, animState);
			manager.AddComponentData(entity, default(AnimationTextureCoordinate));

			var renderCharacter = new RenderCharacter
			{
				Material = renderer.sharedMaterial,
				AnimationTexture = bakedData.AnimationTextures,
				Mesh = bakedData.NewMesh,
				ReceiveShadows = renderer.receiveShadows,
				CastShadows = renderer.shadowCastingMode
				
			};
			manager.AddSharedComponentData(entity, renderCharacter);
		}
	}
    public class ConvertToGPUCharacter : MonoBehaviour, IConvertGameObjectToEntity
    {
		public AnimationClip[] Clips;
		public float Framerate = 60.0F;
		
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            CharacterUtility.AddCharacterComponents(dstManager, entity, gameObject, Clips, Framerate);
        }
    }
}