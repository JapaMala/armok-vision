﻿using System.Xml.Linq;
using UnityEngine;
using System.Linq;

abstract public class ContentConfiguration<T> where T : IContent, new()
{
    protected class Content
    {
        public T defaultItem { private get; set; }
        public ContentConfiguration<T> overloadedItem { get; set; }
        public T GetValue(MapTile tile)
        {
            if (overloadedItem == null)
                return defaultItem;
            else
            {
                T item;
                if (overloadedItem.GetValue(tile, out item))
                {
                    return item;
                }
                else
                    return defaultItem;
            }
        }
    }
    abstract public bool GetValue(MapTile tile, out T value);

    abstract protected void ParseElementConditions(XElement elemtype, Content content);

    string nodeName { get; set; }

    void ParseContentElement(XElement elemtype)
    {
        T value = new T();
        if (!value.AddTypeElement(elemtype))
        {
            Debug.LogError("Couldn't parse " + elemtype);
            //There was an error parsing the type
            //There's nothing to work with.
            return;
        }
        Content content = new Content();
        content.defaultItem = value;
        ParseElementConditions(elemtype, content);
        if (elemtype.Element("subObject") != null)
        {
            content.overloadedItem = GetFromRootElement(elemtype, "subObject");
            content.overloadedItem.AddSingleContentConfig(elemtype);
        }
    }

    public bool AddSingleContentConfig(XElement elemRoot)
    {
        var elemValues = elemRoot.Elements(nodeName);
        foreach (XElement elemValue in elemValues)
        {
            ParseContentElement(elemValue);
        }
        return true;
    }

    public static ContentConfiguration<T> GetFromRootElement(XElement elemRoot, XName name)
    {
        ContentConfiguration<T> output;
        switch (elemRoot.Element(name).Elements().First().Name.LocalName)
        {
            case "material":
                output = new MaterialConfiguration<T>();
                break;
            case "tiletype":
                output = new TileConfiguration<T>();
                break;
            default:
                Debug.LogError("Found unknown matching method \"" + elemRoot.Elements().First().Elements().First().Name.LocalName + "\", assuming material.");
                output = new MaterialConfiguration<T>();
                break;
        }
        output.nodeName = name.LocalName;
        return output;
    }
}

