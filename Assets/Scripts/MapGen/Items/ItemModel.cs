﻿using Building;
using DF.Flags;
using RemoteFortressReader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DF.Enums;

public class ItemModel : MonoBehaviour, IClickable
{
    public Item originalItem;
    public MaterialDefinition itemRaw;
    private MeshRenderer meshRenderer;
    private Material originalMaterial;

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        foreach (var item in GetComponentsInChildren<Collider>())
        {
            if (item.GetComponent<BuildingSelect>() == null)
            {
                item.gameObject.AddComponent<BuildingSelect>().root = this;
            }
        }
    }

    bool phantom = false;

    public void UpdateMaterial(Item itemInput, UnitDefinition unit = null)
    {
        originalItem = itemInput;
        if(ItemRaws.Instance.ContainsKey(itemInput.type))
        {
            itemRaw = ItemRaws.Instance[itemInput.type];
        }
        else
        {
            itemRaw = ItemRaws.Instance[new MatPairStruct(itemInput.type.mat_type, -1)];
        }
        if (phantom)
            return;
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();

        //This means that it's just a placeholder on a body part.
        if (meshRenderer == null)
        {
            phantom = true;
            return;
        }
        if (originalMaterial == null)
            originalMaterial = meshRenderer.sharedMaterial;

        Color partColor = ContentLoader.GetColor(itemInput);
        float textureIndex = ContentLoader.GetPatternIndex(itemInput.material);
        float shapeIndex = ContentLoader.GetShapeIndex(itemInput.material);

        meshRenderer.sharedMaterial = ContentLoader.getFinalMaterial(originalMaterial, partColor.a);

        if(itemInput.type.mat_type == 53 && originalMaterial.shader.name == "Art/SingleImage") //plant. We have colored sprites for these.
            partColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        if (((ItemFlags)(itemInput.flags1) & ItemFlags.rotten) == ItemFlags.rotten)
            partColor = new Color(0, 0, 0, 0.5f);



        MaterialPropertyBlock prop = new MaterialPropertyBlock();
        prop.SetColor("_MatColor", partColor);
        prop.SetFloat("_MatIndex", textureIndex);
        prop.SetFloat("_ShapeIndex", shapeIndex);
        if (unit != null)
            prop.SetColor("_JobColor", unit.profession_color);
        if (ImageManager.Instance != null)
            prop.SetFloat("_SpriteIndex", ImageManager.Instance.GetItemTile(itemInput.type));
        meshRenderer.SetPropertyBlock(prop);


        UpdateImprovements(gameObject, itemInput);

        var image = GetComponentInChildren<ItemImage>();
        if (image != null)
            image.UpdateImage(itemInput);
    }

    public static void UpdateImprovements(GameObject GO, Item itemInput)
    {
        List<RemoteFortressReader.ItemImprovement> images = new List<RemoteFortressReader.ItemImprovement>();
        List<RemoteFortressReader.ItemImprovement> ringSpikeBands = new List<RemoteFortressReader.ItemImprovement>();
        List<RemoteFortressReader.ItemImprovement> covereds = new List<RemoteFortressReader.ItemImprovement>();
        List<RemoteFortressReader.ItemImprovement> specifics = new List<RemoteFortressReader.ItemImprovement>();

        foreach (var improvement in itemInput.improvements)
        {
            switch ((ImprovementType)improvement.type)
            {
                case ImprovementType.ArtImage:
                    images.Add(improvement);
                    break;
                case ImprovementType.Covered:
                    covereds.Add(improvement);
                    break;
                case ImprovementType.RingsHanging:
                case ImprovementType.Bands:
                case ImprovementType.Spikes:
                    ringSpikeBands.Add(improvement);
                    break;
                case ImprovementType.Thread:
                case ImprovementType.Cloth:
                    //Handled already, in various ways.
                    break;
                case ImprovementType.Writing:
                    break; //Not rendered, currently.
                case ImprovementType.Itemspecific:
                case ImprovementType.Pages:
                    specifics.Add(improvement);
                    break;
                case ImprovementType.SewnImage:
                case ImprovementType.Illustration:
                case ImprovementType.InstrumentPiece:
                default:
//#if UNITY_EDITOR
//                    Debug.LogWarning(string.Format("Unhandled improvement {0} on {1}", improvement.type, GO.name));
//#endif
                    break;
            }
        }

        var imps = GO.GetComponentsInChildren<ItemImprovement>();
        for (int i = 0; i < imps.Length; i++)
        {
            var imp = imps[i];
            if (imp.isImage && imp.index < images.Count)
            {
                imp.UpdateImprovement(images[imp.index]);
            }
            else if (!imp.isImage && imp.index < ringSpikeBands.Count && (ImprovementType)ringSpikeBands[imp.index].type == ImprovementType.RingsHanging)
            {
                imp.UpdateImprovement(ringSpikeBands[imp.index]);
            }
            else if (!imp.isImage && imp.index < ringSpikeBands.Count && (ImprovementType)ringSpikeBands[imp.index].type == ImprovementType.Spikes)
            {
                imp.UpdateImprovement(ringSpikeBands[imp.index]);
            }
            else if (!imp.isImage && imp.index < ringSpikeBands.Count && (ImprovementType)ringSpikeBands[imp.index].type == ImprovementType.Bands)
            {
                imp.UpdateImprovement(ringSpikeBands[imp.index]);
            }
            else if (covereds.Count > 0)
            {
                Random.InitState(i);
                imp.UpdateImprovement(covereds[Random.Range(0, covereds.Count)]);
            }
            //else
            //    imp.gameObject.SetActive(false);
        }
        foreach (var sub in GO.GetComponentsInChildren<ItemSubPart>())
        {
            if (sub.partIndex < 0 || sub.partIndex >= specifics.Count)
            {
                sub.gameObject.SetActive(false);
                continue;
            }
            sub.UpdateImprovement(specifics[sub.partIndex]);
        }
    }

    public void HandleClick()
    {
        PrintItemInfo(originalItem);
    }

    public static void PrintItemInfo(Item item)
    {
        string mat = ((MatPairStruct)(item.material)).ToString();
        if (MaterialRaws.Instance.ContainsKey(item.material))
            mat = MaterialRaws.Instance[item.material].id;
        if (item.stack_size > 1)
            Debug.Log(string.Format("{0} {1} [{2}]", mat, ItemRaws.Instance[item.type].id, item.stack_size));
        else
            Debug.Log(string.Format("{0} {1}", mat, ItemRaws.Instance[item.type].id));
        Debug.Log(((ItemFlags)item.flags1));

        foreach (var imp in item.improvements)
        {
            mat = ((MatPairStruct)(imp.material)).ToString();
            if (MaterialRaws.Instance.ContainsKey(imp.material))
                mat = MaterialRaws.Instance[imp.material].id;
            Debug.Log(string.Format("    {0} {1}", mat, imp.type));
        }
    }
}
