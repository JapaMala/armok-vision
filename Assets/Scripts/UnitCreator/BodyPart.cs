﻿using RemoteFortressReader;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;

[SelectionBase]
public class BodyPart : MonoBehaviour
{
    public string token;
    public string category;
    public VolumeKeeper placeholder;
    public BodyPartFlags flags;
    [SerializeField]
    private Bounds bounds;
    public float volume;
    public List<BodyPartLayerRaw> layers = new List<BodyPartLayerRaw>();
    public List<IBodyLayer> layerModels = new List<IBodyLayer>();

    [NonSerialized]
    public BodyPart parent;
    [NonSerialized]
    public List<BodyPart> children = new List<BodyPart>();

    internal BodyPartModel modeledPart;
    [Serializable]
    public struct ModValue
    {
        public ModValue(BpAppearanceModifier mod, int value)
        {
            type = mod.type;
            this.value = value;
        }
        public string type;
        public int value;

        public override string ToString()
        {
            return type;
        }
    }

    [Serializable]
    public struct Equip
    {
        public string itemType;
        public InventoryItem item;
        public MaterialDefinition itemDef;
        public MaterialDefinition material;

        public Equip(InventoryItem item, MaterialDefinition itemDef, MaterialDefinition material)
        {
            if (itemDef != null)
                itemType = itemDef.id.Split('/').Last();
            else
                itemType = item.item.type.ToString();
            this.item = item;
            this.itemDef = itemDef;
            this.material = material;
        }
    }

    public List<ModValue> mods = new List<ModValue>();
    public List<Equip> inventory = new List<Equip>();
    private MatPairStruct heldItemType = new MatPairStruct(-1,-1);
    private ItemModel heldItemModel;

    internal BodyPart FindChild(string category)
    {
        foreach (Transform child in transform)
        {
            var childPart = child.GetComponent<BodyPart>();
            if (childPart == null)
                continue;
            if (childPart.category == category)
                return childPart;
        }
        return null;
    }

    class ChildPlacement
    {
        public string category;
        public bool categoryRegex;
        public string token;
        public bool tokenRegex;
        public Transform start;
        public Transform end;
        public Transform single;
        bool kill = false;

        List<BodyPart> bodyParts = new List<BodyPart>();

        public ChildPlacement(BodyPartChildPlaceholder placeholder)
        {
            category = placeholder.category;
            categoryRegex = placeholder.categoryRegex;
            token = placeholder.token;
            tokenRegex = placeholder.tokenRegex;
            Add(placeholder);
        }

        public void Add(BodyPartChildPlaceholder placeholder)
        {
            if (kill)
                return;
            switch (placeholder.placement)
            {
                case BodyPartChildPlaceholder.PlacementCategory.Singular:
                    single = placeholder.transform;
                    break;
                case BodyPartChildPlaceholder.PlacementCategory.ArrayStart:
                    start = placeholder.transform;
                    break;
                case BodyPartChildPlaceholder.PlacementCategory.ArrayEnd:
                    end = placeholder.transform;
                    break;
                case BodyPartChildPlaceholder.PlacementCategory.Kill:
                    kill = true;
                    break;
            }
        }

        public bool Matches(BodyPartChildPlaceholder placeholder)
        {
            return
                category == placeholder.category &&
                categoryRegex == placeholder.categoryRegex &&
                token == placeholder.token &&
                tokenRegex == placeholder.tokenRegex;
        }

        public bool Matches(BodyPart part)
        {
            if (!string.IsNullOrEmpty(category))
            {
                if (categoryRegex)
                {
                    if (!Regex.IsMatch(part.category, category))
                        return false;
                }
                else
                {
                    if (category != part.category)
                        return false;
                }
            }
            if (!string.IsNullOrEmpty(token))
            {
                if (tokenRegex)
                {
                    if (!Regex.IsMatch(part.token, token))
                        return false;
                }
                else
                {
                    if (token != part.token)
                        return false;
                }
            }
            return true;
        }

        internal void Place(BodyPart childPart)
        {
            throw new NotImplementedException();
        }

        internal void Add(BodyPart childPart)
        {
            if (kill)
                childPart.gameObject.SetActive(false);
            else
                bodyParts.Add(childPart);
        }

        internal void Arrange()
        {
            //This shouldn't be, but just in case.
            if (kill)
            {
                foreach (var item in bodyParts)
                {
                    item.gameObject.SetActive(false);
                }
                return;
            }
            if (bodyParts.Count == 0)
                return;
            if(bodyParts.Count == 1)
            {
                if (single != null)
                {
                    bodyParts[0].transform.position = single.position;
                    bodyParts[0].transform.rotation = single.rotation;
                    bodyParts[0].transform.localScale = single.localScale;
                }
                else
                    SetTransform(bodyParts[0].transform, 0.5f);
            }
            else
            {
                for(int i = 0; i < bodyParts.Count; i++)
                {
                    SetTransform(bodyParts[i].transform, i / (float)(bodyParts.Count - 1));
                }
            }
        }

        private void SetTransform(Transform trans, float t)
        {
            var tempStart = start;
            var tempEnd = end;
            if(tempStart == null)
            {
                if (end != null)
                    tempStart = end;
                else
                    tempStart = single;
            }
            if(tempEnd == null)
            {
                if (start != null)
                    tempEnd = start;
                else
                    tempEnd = single;
            }
            trans.position = Vector3.Lerp(tempStart.position, tempEnd.position, t);
            trans.rotation = Quaternion.Lerp(tempStart.rotation, tempEnd.rotation, t);
            trans.localScale = Vector3.Lerp(tempStart.localScale, tempEnd.localScale, t);
        }
    }

    internal bool TryFindMod(string modToken, out ModValue mod)
    {
        int modIndex = mods.FindIndex(x => x.type == modToken);
        if(modIndex >= 0)
        {
            mod = mods[modIndex];
            return true;
        }
        mod = default;
        return false;
    }

    internal bool TryFindModTree(string modToken, out ModValue mod)
    {
        if(modToken.Contains('/')) //Eg HEAD/HAIR/LENGTH
        {
            var tree = modToken.Split(new char[] { '/' }, 2);
            if(tree[1].Contains('/'))
            {
                int childPartIndex = children.FindIndex(x => x.category == tree[0]);
                if(childPartIndex >= 0)
                {
                    return children[childPartIndex].TryFindModTree(tree[1], out mod);
                }
            }
            else
            {
                int layerIndex = layerModels.FindIndex(x => x.RawLayerName == tree[0]);
                if (layerIndex >= 0)
                {
                    var layer = layerModels[layerIndex];
                    return layer.TryFindMod(tree[1], out mod);
                }
            }
        }
        mod = default;
        return false;
    }

    void ArrangeModeledPart(CreatureBody body)
    {
        if (modeledPart == null)
            return;

        var partSize = Vector3.one;

        foreach (var mod in mods)
        {
            var value = 100;
                value = mod.value;
            switch (mod.type)
            {
                case "BROADNESS":
                    partSize.x = value / 100f;
                    break;
                case "HEIGHT":
                case "ROUND_VS_NARROW":
                    partSize.y = value / 100f;
                    break;
                case "LENGTH":
                    partSize.z = value / 100f;
                    break;
                case "CLOSE_SET":
                    modeledPart.transform.localPosition = new Vector3(Mathf.Abs(transform.localPosition.x * (value / 100f - 1)) / 2.0f, 0, 0);
                    break;
                case "SPLAYED_OUT":
                    modeledPart.transform.localRotation = Quaternion.Euler(0, -value / 200f * 90, 0);
                    break;
                default:
                    break;
            }
        }

        //If the're flagged as small, they don't need to worry about volume. Just need to scale according to the parent.
        if(flags.small)
        {
            modeledPart.transform.localScale = partSize;
        }
        if (!flags.small)
        {
            modeledPart.transform.localScale = MultiplyScales(body.bodyScale, partSize);
            modeledPart.volume = volume;
            modeledPart.FixVolume();
        }
        bounds = modeledPart.GetComponentInChildren<MeshRenderer>().bounds;
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            bounds.Encapsulate(renderer.bounds);
        }
        List<ChildPlacement> placements = new List<ChildPlacement>();
        foreach (Transform child in modeledPart.transform)
        {
            BodyPartChildPlaceholder bodyPartChild = child.GetComponent<BodyPartChildPlaceholder>();
            if (bodyPartChild == null)
                continue;
            if(bodyPartChild.category == ":ATTACH:")
            {
                heldItemPoint = child;
            }
            bool placedPart = false;
            foreach (var placement in placements)
            {
                if(placement.Matches(bodyPartChild))
                {
                    placement.Add(bodyPartChild);
                    placedPart = true;
                }
            }
            if (!placedPart)
            {
                placements.Add(new ChildPlacement(bodyPartChild));
            }
        }

        List<BodyPart> childParts = new List<BodyPart>();

        foreach (Transform child in transform)
        {
            var childPart = child.GetComponent<BodyPart>();
            if (childPart == null)
                continue;
            childParts.Add(childPart);
        }
        foreach (var childPart in childParts)
        {
            if (childPart.flags.small && childPart.modeledPart != null)
            {
                //Its size doesn't matter if it's considered small, so it should just take the scale directly from the parent part.
                //Also, this only applies to parts that actually have models defined. Procedural parts still use the old system.
                childPart.transform.SetParent(modeledPart.transform, false);
            }
            bool placed = false;
            foreach (var placement in placements)
            {
                if (placement.Matches(childPart))
                {
                    placement.Add(childPart);
                    placed = true;
                    break;
                }
            }
            if (!placed)
                childPart.gameObject.SetActive(false);
        }
        foreach (var placement in placements)
        {
            placement.Arrange();
        }
        foreach (var childPart in childParts)
        {
            childPart.Arrange(body);
        }
    }

    public void Arrange(CreatureBody body)
    {
        if (modeledPart != null)
        {
            ArrangeModeledPart(body);
            return;
        }
        if (placeholder == null)
            return;
        Shapen(body);
        List<BodyPart> toes = new List<BodyPart>();
        List<BodyPart> fingers = new List<BodyPart>();
        List<BodyPart> mouthParts = new List<BodyPart>();
        BodyPart mouth = null;
        BodyPart beak = null;
        List<BodyPart> leftLegs = new List<BodyPart>();
        List<BodyPart> rightLegs = new List<BodyPart>();
        List<BodyPart> multiArms = new List<BodyPart>();
        List<BodyPart> tentacles = new List<BodyPart>();
        List<BodyPart> centerEyes = new List<BodyPart>();
        foreach (Transform child in transform)
        {
            var childPart = child.GetComponent<BodyPart>();
            if (childPart == null)
                continue;
            childPart.Arrange(body);
            switch (childPart.category)
            {
                case "BODY_LOWER":
                    if (body.bodyCategory == CreatureBody.BodyCategory.Humanoid)
                        childPart.transform.localPosition = new Vector3(0, bounds.min.y, 0);
                    else
                        childPart.transform.localPosition = new Vector3(0, 0, bounds.min.z);
                    break;
                case "LEG_UPPER":
                case "LEG":
                    childPart.transform.localPosition = new Vector3(bounds.extents.x / 2 * (childPart.flags.left ? -1 : 1), bounds.min.y, 0);
                    break;
                case "FOOT":
                case "FOOT_REAR":
                case "FOOT_FRONT":
                case "HOOF":
                case "HOOF_REAR":
                case "HOOF_FRONT":
                    childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    break;
                case "TOE":
                    toes.Add(childPart);
                    break;
                case "FINGER":
                    if (childPart.token.EndsWith("1")) //It's a thumb
                    {
                        childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + childPart.bounds.extents.z);
                    }
                    else
                        fingers.Add(childPart);
                    break;
                case "NECK":
                    switch (body.bodyCategory)
                    {
                        case CreatureBody.BodyCategory.Humanoid:
                            childPart.transform.localPosition = new Vector3(0, bounds.max.y, 0);
                            break;
                        case CreatureBody.BodyCategory.Bug:
                        case CreatureBody.BodyCategory.Fish:
                            childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
                            childPart.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                            break;
                        case CreatureBody.BodyCategory.Quadruped:
                        case CreatureBody.BodyCategory.Avian:
                        default:
                            childPart.transform.localPosition = new Vector3(0, bounds.max.y, bounds.max.z - childPart.bounds.extents.z);
                            break;
                    }
                    break;
                case "ARM_UPPER":
                case "ARM":
                    if (!childPart.flags.left && !childPart.flags.right)
                    {
                        multiArms.Add(childPart);
                    }
                    else
                    {
                        childPart.transform.localPosition = new Vector3((bounds.extents.x + childPart.bounds.extents.x) * (childPart.flags.left ? -1 : 1), bounds.max.y - childPart.bounds.extents.x, 0);
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.down, new Vector3(child.transform.localPosition.x, 0, 0));
                    }
                    break;
                case "ARM_LOWER":
                case "LEG_LOWER":
                case "HAND":
                    childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    break;
                case "TENTACLE":
                    tentacles.Add(childPart);
                    break;
                case "HEAD":
                    if (category == "NECK")
                    {
                        switch (body.bodyCategory)
                        {
                            case CreatureBody.BodyCategory.Fish:
                            case CreatureBody.BodyCategory.Bug:
                                childPart.transform.localPosition = new Vector3(0, bounds.max.y + childPart.bounds.extents.z, bounds.center.z + childPart.bounds.center.y);
                                childPart.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
                                break;
                            case CreatureBody.BodyCategory.Humanoid:
                            case CreatureBody.BodyCategory.Quadruped:
                            case CreatureBody.BodyCategory.Avian:
                            default:
                                childPart.transform.localPosition = new Vector3(0, bounds.max.y, 0);
                                break;
                        }
                    }
                    else
                    {
                        switch (body.bodyCategory)
                        {
                            case CreatureBody.BodyCategory.Humanoid:
                                childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                                break;
                            case CreatureBody.BodyCategory.Bug:
                            case CreatureBody.BodyCategory.Fish:
                                childPart.transform.localPosition = new Vector3(0, bounds.center.y - childPart.bounds.center.y, bounds.max.z - childPart.bounds.min.z);
                                break;
                            case CreatureBody.BodyCategory.Quadruped:
                            case CreatureBody.BodyCategory.Avian:
                            default:
                                childPart.transform.localPosition = new Vector3(0, bounds.max.y, bounds.max.z - childPart.bounds.extents.z);
                                break;
                        }
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    }
                    break;
                case "EYELID":
                case "EYE":
                    if (childPart.token.StartsWith("R"))
                        childPart.transform.localPosition = new Vector3(bounds.center.x - bounds.extents.x / 2, bounds.center.y, bounds.max.z);
                    else if (childPart.token.StartsWith("L"))
                        childPart.transform.localPosition = new Vector3(bounds.center.x + bounds.extents.x / 2, bounds.center.y, bounds.max.z);
                    else
                        centerEyes.Add(childPart);
                    break;
                case "MOUTH":
                    childPart.transform.localPosition = new Vector3(0, bounds.min.y, bounds.max.z - childPart.bounds.max.z);
                    mouth = childPart;
                    break;
                case "BEAK":
                    childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y - childPart.bounds.min.y, bounds.max.z);
                    mouth = childPart;
                    break;
                case "NOSE":
                    childPart.transform.localPosition = new Vector3(0, bounds.center.y - (bounds.extents.y / 2), bounds.max.z);
                    break;
                case "EAR":
                    childPart.transform.localPosition = new Vector3(bounds.center.x + (bounds.extents.x * (childPart.flags.left ? -1 : 1)), bounds.center.y, bounds.center.z);
                    childPart.transform.localRotation = Quaternion.LookRotation(new Vector3(child.transform.localPosition.x, 0, 0), Vector3.up);
                    break;
                case "CHEEK":
                case "TONGUE":
                case "LIP":
                case "TOOTH":
                case "TUSK":
                    mouthParts.Add(childPart);
                    break;
                case "LEG_FRONT":
                case "LEG_REAR":
                    if (childPart.token.StartsWith("L"))
                        leftLegs.Add(childPart);
                    else
                        rightLegs.Add(childPart);
                    break;
                case "WING":
                    childPart.transform.localPosition = new Vector3(bounds.extents.x * (childPart.flags.left ? -1 : 1), bounds.max.y, bounds.center.z);
                    childPart.transform.localRotation = Quaternion.Euler(-30,0,0);
                    break;
                case "TAIL":
                    if(body.bodyCategory == CreatureBody.BodyCategory.Humanoid)
                    {
                        childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.min.z);
                        childPart.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -1, -1), Vector3.up);
                    }
                    else
                    {
                        childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.min.z);
                        childPart.transform.localRotation = Quaternion.Euler(135, 0, 0);
                    }
                    break;
                case "STINGER":
                    childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    childPart.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -1, 1), Vector3.forward);
                    break;
                case "ANTENNA":
                    {
                        bool left = childPart.token.StartsWith("L");
                        childPart.transform.localPosition = new Vector3(bounds.center.x + (bounds.extents.x * (left ? -1 : 1)), bounds.max.y, bounds.max.z);
                        childPart.transform.localRotation = Quaternion.Euler(30, left ? -15 : 15, 0);
                    }
                    break;
                case "HORN":
                    {
                        bool left = childPart.token.StartsWith("L");
                        childPart.transform.localPosition = new Vector3(bounds.center.x + (bounds.extents.x * (left ? -1 : 1)), bounds.max.y, bounds.center.z);
                        childPart.transform.localRotation = Quaternion.Euler(30, left ? -15 : 15, 0);
                    }
                    break;
                case "SHELL":
                case "HUMP":
                    if (body.bodyCategory != CreatureBody.BodyCategory.Humanoid)
                    {
                        if(FindChild("BODY_LOWER") == null)
                            childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                        else
                            childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.min.z);
                    }
                    else
                    {
                        childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
                    }
                    break;
                case "FIN":
                    if(childPart.token.StartsWith("R"))
                    {
                        childPart.transform.localPosition = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.forward);
                    }
                    else if(childPart.token.StartsWith("L"))
                    {
                        childPart.transform.localPosition = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.forward);
                    }
                    else
                    {
                        childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                        childPart.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                    }
                    break;
                case "PINCER":
                    childPart.transform.localPosition = new Vector3(bounds.min.x * (childPart.flags.left ? -1 : 1), bounds.center.y, bounds.max.z);
                    childPart.transform.localRotation = Quaternion.LookRotation(new Vector3(childPart.flags.left ? 1 : -1, 0, 1));
                    break;
                case "FLIPPER":
                    childPart.transform.localPosition = new Vector3(bounds.min.x * (childPart.flags.left ? -1 : 1), bounds.center.y, bounds.max.z);
                    break;
                default:
                    childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
                    break;
            }
            if (childPart.flags.left && !flags.left)
                childPart.transform.localScale = new Vector3(-1, 1, 1);
        }
        for (int i = 0; i < toes.Count; i++)
        {
            float basecoord = bounds.min.x;
            float step = bounds.size.x / toes.Count;
            toes[i].transform.localPosition = new Vector3(basecoord + step / 2 + step * i, bounds.min.y - toes[i].bounds.min.y, bounds.max.z);
        }
        for (int i = 0; i < fingers.Count; i++)
        {
            float basecoord = bounds.min.z;
            float step = bounds.size.z / fingers.Count;
            fingers[i].transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, basecoord + step / 2 + step * i);
        }
        if(mouth != null)
            foreach (var childPart in mouthParts)
            {
                switch (childPart.category)
                {
                    case "TONGUE":
                        childPart.transform.SetParent(mouth.transform, false);
                        childPart.transform.localPosition = new Vector3(0, 0, mouth.bounds.max.z - mouth.bounds.extents.y);
                        break;
                    case "LIP":
                        if (childPart.token.StartsWith("L"))
                        {
                            childPart.transform.SetParent(mouth.transform, false);
                            childPart.transform.localPosition = new Vector3(0, mouth.bounds.max.y, mouth.bounds.max.z);
                        }
                        else
                        {
                            childPart.transform.localPosition = new Vector3(0, bounds.min.y, bounds.max.z);
                        }
                        break;
                    case "CHEEK":
                        childPart.transform.localPosition = new Vector3(bounds.center.x + (mouth.bounds.extents.x * (childPart.token.StartsWith("L") ? -1 : 1)), bounds.min.y, bounds.max.z);
                        if (childPart.token.StartsWith("L"))
                            childPart.transform.localScale = new Vector3(-1, 1, 1);
                        break;
                    case "TOOTH":
                        if(childPart.token.StartsWith("U_F_"))
                        {
                            childPart.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y - mouth.bounds.extents.y * 0.01f, bounds.max.z - childPart.bounds.extents.z);
                            childPart.transform.localScale = new Vector3(1, -1, 1);
                        }
                        else if (childPart.token.StartsWith("L_F_"))
                        {
                            childPart.transform.SetParent(mouth.transform);
                            childPart.transform.localPosition = new Vector3(mouth.bounds.center.x, mouth.bounds.max.y + mouth.bounds.extents.y * 0.01f, mouth.bounds.max.z - childPart.bounds.extents.z);
                        }
                        else if (childPart.token.StartsWith("U_R_B_"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.max.x - childPart.bounds.extents.z, bounds.min.y - mouth.bounds.extents.y * 0.50f, bounds.max.z - childPart.bounds.extents.x);
                            childPart.transform.localScale = new Vector3(1, -1, 1);
                            childPart.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        }
                        else if (childPart.token.StartsWith("U_L_B_"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.min.x + childPart.bounds.extents.z, bounds.min.y - mouth.bounds.extents.y * 0.01f, bounds.max.z - childPart.bounds.extents.x);
                            childPart.transform.localScale = new Vector3(1, -1, -1);
                            childPart.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        }
                        else if (childPart.token.StartsWith("L_R_B_"))
                        {
                            childPart.transform.SetParent(mouth.transform, false);
                            childPart.transform.localPosition = new Vector3(mouth.bounds.max.x - childPart.bounds.extents.z, mouth.bounds.max.y + mouth.bounds.extents.y * 0.01f, mouth.bounds.max.z - childPart.bounds.extents.x);
                            childPart.transform.localScale = new Vector3(1, 1, 1);
                            childPart.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        }
                        else if (childPart.token.StartsWith("L_L_B_"))
                        {
                            childPart.transform.SetParent(mouth.transform, false);
                            childPart.transform.localPosition = new Vector3(mouth.bounds.min.x + childPart.bounds.extents.z, mouth.bounds.max.y + mouth.bounds.extents.y * 0.01f, mouth.bounds.max.z - childPart.bounds.extents.x);
                            childPart.transform.localScale = new Vector3(1, 1, -1);
                            childPart.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        }
                        else if (childPart.token.StartsWith("R_EYE"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.max.x - childPart.bounds.extents.z, bounds.min.y, bounds.max.z);
                            childPart.transform.localScale = new Vector3(1, 1, 1);
                            childPart.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
                        }
                        else if (childPart.token.StartsWith("L_EYE"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.min.x + childPart.bounds.extents.z, bounds.min.y, bounds.max.z);
                            childPart.transform.localScale = new Vector3(-1, 1, 1);
                            childPart.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
                        }
                        break;
                    case "TUSK":
                        if (childPart.token.StartsWith("R"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.max.x - childPart.bounds.extents.z, bounds.min.y, bounds.max.z);
                            childPart.transform.localScale = new Vector3(1, 1, 1);
                        }
                        else if (childPart.token.StartsWith("L"))
                        {
                            childPart.transform.localPosition = new Vector3(mouth.bounds.min.x + childPart.bounds.extents.z, bounds.min.y, bounds.max.z);
                            childPart.transform.localScale = new Vector3(-1, 1, 1);
                        }
                        break;
                    default:
                        break;
                }
            }
        else if(beak != null)
        {
            foreach (var childPart in mouthParts)
            {
                switch (childPart.category)
                {
                    case "TONGUE":
                        childPart.transform.SetParent(beak.transform, false);
                        childPart.transform.localPosition = bounds.center;
                        break;
                    default:
                        break;
                }
            }
        }

        float legZ = bounds.min.z;
        if (category == "BODY_UPPER")
        {
            legZ = bounds.max.z;
        }

        switch (body.bodyCategory)
        {
            case CreatureBody.BodyCategory.Bug:
                AlignManyParts(
                     leftLegs,
                     new Vector3(bounds.min.x, bounds.center.y, bounds.max.z),
                     Quaternion.Euler(-60, -45, 0),
                     new Vector3(bounds.min.x, bounds.center.y, bounds.min.z),
                     Quaternion.Euler(-60, -135, 0),
                     0);
                AlignManyParts(
                     rightLegs,
                     new Vector3(bounds.max.x, bounds.center.y, bounds.max.z),
                     Quaternion.Euler(-60, 45, 0),
                     new Vector3(bounds.max.x, bounds.center.y, bounds.min.z),
                     Quaternion.Euler(-60, 135, 0),
                     0);
                break;
            case CreatureBody.BodyCategory.Humanoid:
            case CreatureBody.BodyCategory.Quadruped:
            case CreatureBody.BodyCategory.Avian:
            default:
                AlignManyParts(
                    leftLegs,
                    new Vector3(bounds.min.x, bounds.max.y, legZ),
                    Quaternion.identity,
                    new Vector3(bounds.min.x, bounds.max.y, legZ),
                    Quaternion.identity,
                    0);
                AlignManyParts(
                    rightLegs,
                    new Vector3(bounds.max.x, bounds.max.y, legZ),
                    Quaternion.identity,
                    new Vector3(bounds.max.x, bounds.max.y, legZ),
                    Quaternion.identity,
                    0);
                break;
        }
        AlignManyParts(multiArms,
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            Quaternion.LookRotation(new Vector3(1, -0.5f, 1)),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            Quaternion.LookRotation(new Vector3(-1, -0.5f, 1)),
            0.5f);
        AlignManyParts(tentacles,
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            Quaternion.LookRotation(new Vector3(1, -1, 1)),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            Quaternion.LookRotation(new Vector3(-1, -1, 1)),
            0.5f);
        AlignManyParts(centerEyes,
            new Vector3(bounds.center.x + bounds.extents.x, bounds.center.y + bounds.extents.y / 2, bounds.max.z),
            Quaternion.identity,
            new Vector3(bounds.center.x - bounds.extents.x, bounds.center.y + bounds.extents.y / 2, bounds.max.z),
            Quaternion.identity,
            0.5f);
    }

    Transform heldItemPoint = null;

    internal void UpdateItems(UnitDefinition unit)
    {
        var heldItemIndex = inventory.FindLastIndex(x => x.item.mode == InventoryMode.Hauled || x.item.mode == InventoryMode.Weapon);
        if(heldItemIndex >= 0)
        {
            var heldItem = inventory[heldItemIndex];
            if(heldItem.item.item.type != heldItemType)
            {
                if (heldItemModel != null)
                    Destroy(heldItemModel.gameObject);
                var point = heldItemPoint;
                if (point == null)
                    point = transform;
                heldItemModel = ItemManager.InstantiateItem(heldItem.item.item, transform, false);
                heldItemModel.transform.position = point.transform.position;
                heldItemModel.transform.rotation = point.transform.rotation;
                heldItemModel.transform.localScale *= 1.0f / transform.lossyScale.magnitude;
                heldItemType = heldItem.item.item.type;
            }
        }
        else
        {
            if (heldItemModel != null)
                Destroy(heldItemModel.gameObject);
            heldItemModel = null;
        }
        if (modeledPart != null)
        {
            modeledPart.ApplyEquipment(inventory, unit);
        }
        else
            foreach (var item in inventory)
            {
                if (item.item.mode != InventoryMode.Worn)
                    continue;
                if (flags.head)
                    continue;
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                var color = ContentLoader.GetColor(item.item.item);
                var index = ContentLoader.GetPatternIndex(item.item.item.material);
                propertyBlock.SetColor("_MatColor", color);
                propertyBlock.SetFloat("_MatIndex", index);
                propertyBlock.SetColor("_JobColor", unit.profession_color);
                foreach (var layerModel in layerModels)
                {
                    if (layerModel != null && (layerModel is BodyLayer))
                        ((BodyLayer)layerModel).SetPropertyBlock(propertyBlock);
                }
            }
    }

    void AlignManyParts(List<BodyPart> parts, Vector3 pos1, Quaternion rot1, Vector3 pos2, Quaternion rot2, float singlePos)
    {
        if (parts.Count == 1)
        {
            parts[0].transform.localPosition = Vector3.Lerp(pos1, pos2, singlePos);
            parts[0].transform.localRotation = Quaternion.Lerp(rot1, rot2, singlePos);
        }
        else
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].transform.localPosition = Vector3.Lerp(pos1, pos2, (float)i / (parts.Count-1));
                parts[i].transform.localRotation = Quaternion.Lerp(rot1, rot2, (float)i / (parts.Count - 1));
            }
    }

    public void Shapen(CreatureBody body)
    {
        switch (category)
        {
            case "BODY_UPPER":
                if (body.bodyCategory == CreatureBody.BodyCategory.Humanoid)
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1.5f, 1.5f, 1));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                }
                else
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1.5f, 1, 1.5f));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                }
                break;
            case "BODY_LOWER":
                if (body.bodyCategory == CreatureBody.BodyCategory.Humanoid)
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1.5f, 1.5f, 1));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                }
                else
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1.5f, 1, 1.5f));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, 0, -placeholder.transform.localScale.z / 2);
                }
                break;
            case "ARM_UPPER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.75f, 2f, 0.75f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -(placeholder.transform.localScale.y / 2) + (placeholder.transform.localScale.x / 2), 0);
                break;
            case "ARM":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.75f, 4f, 0.75f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -(placeholder.transform.localScale.y / 2) + (placeholder.transform.localScale.x / 2), 0);
                break;
            case "TENTACLE":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.75f, 0.75f, 4f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                break;
            case "LEG_UPPER":
            case "LEG_LOWER":
            case "ARM_LOWER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.75f, 2f, 0.75f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "LEG":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.75f, 0.75f, 4f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                break;
            case "LEG_FRONT":
            case "LEG_REAR":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 4, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "FOOT":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(0.5f, 0.25f, 1f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, (placeholder.transform.localScale.z / 2) - (placeholder.transform.localScale.x / 2));
                break;
            case "FOOT_REAR":
            case "FOOT_FRONT":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 1, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, (placeholder.transform.localScale.z / 2) - (placeholder.transform.localScale.x / 2));
                break;
            case "TOE":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 1, 2));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                break;
            case "HAND":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(2, 6, 5));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "FINGER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 4, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "HEAD":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 1, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                break;
            case "MOUTH":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(3.5f, 1, 2));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, placeholder.transform.localScale.z / 2);
                break;
            case "EYE":
            case "EAR":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  Vector3.one);
                placeholder.FixVolume();
                placeholder.transform.localPosition = Vector3.zero;
                break;
            case "EYELID":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(2.5f, 1, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y, placeholder.transform.localScale.z / 2);
                break;
            case "CHEEK":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 3, 4));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(placeholder.transform.localScale.x / 2, 0, -placeholder.transform.localScale.z / 2);
                break;
            case "TONGUE":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1.5f, 1, 2.8f));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, -placeholder.transform.localScale.z / 2);
                break;
            case "LIP":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(5, 1, 1f));
                placeholder.FixVolume();
                if (token.StartsWith("U"))
                    placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                else
                    placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "TOOTH":
                if (token.EndsWith("EYE_TOOTH"))
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 1, 6));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                }
                else
                {
                    placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(6, 1, 1));
                    placeholder.FixVolume();
                    placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                }
                break;
            case "TUSK":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 6, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                break;
            case "WING":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(10, 1, 20));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(-placeholder.transform.localScale.x / 2, 0, placeholder.transform.localScale.z / 2);
                break;
            case "TAIL":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 4, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "STINGER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 5, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            case "ANTENNA":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 8, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                break;
            case "HORN":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 4, 1));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                break;
            case "FIN":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(1, 3, 5));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, placeholder.transform.localScale.z / 2);
                break;
            case "PINCER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(2, 1, 3));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                break;
            case "FLIPPER":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  new Vector3(3, 1, 2));
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(-placeholder.transform.localScale.y / 2, 0, placeholder.transform.localScale.z / 2);
                break;
            case "NOSE":
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, 0, placeholder.transform.localScale.z / 2);
                break;
            case "HOOF":
            case "HOOF_FRONT":
            case "HOOF_REAR":
                placeholder.transform.localScale = MultiplyScales(body.bodyScale, Vector3.one);
                placeholder.FixVolume();
                placeholder.transform.localPosition = new Vector3(0, -placeholder.transform.localScale.y / 2, 0);
                break;
            default:
                placeholder.transform.localScale = MultiplyScales(body.bodyScale,  Vector3.one);
                placeholder.FixVolume();
                if (flags.embedded)
                    placeholder.transform.localPosition = Vector3.zero;
                else
                    placeholder.transform.localPosition = new Vector3(0, placeholder.transform.localScale.y / 2, 0);
                break;
        }
        bounds = new Bounds(placeholder.transform.localPosition, placeholder.transform.localScale);
    }

    public static Vector3 MultiplyScales(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
}
