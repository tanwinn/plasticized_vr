﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using SeniorIS;

[System.Serializable]
public class ObjectGenerator : MonoBehaviour {
    #region Attributes
    [Header("Area Settings")]
    public GameObject wrappedZone = null;
    public bool isWrappedZoneActive = false;
    public GameObject smallZone = null;
    public bool isSmallZoneActive = true;

    [Header("Generator Settings")]
    public bool generateSphere = true;
    public bool generateCube = true;
    public bool generateCylinder = true;

    [Header("Object Settings")]
    public List<float> sizeList = Metadata.sizeList;
    public float RandomHeightMax = Metadata.HEIGHT_MAX;
    public int interactiveCount = Metadata.INTERACTIVE_COUNT;
    public int nonInteractiveCount = Metadata.NONINTERACTIVE_COUNT;

    public bool addForceToObject = false;
    [ConditionalHide("addForceToObject")] public float upForce = 25f;
    [ConditionalHide("addForceToObject")] public float sideForce = 7f;

    public bool boyanceSimulate = false;
    [ConditionalHide("boyanceSimulate")] public float waterLevel = .85f;
    [ConditionalHide("boyanceSimulate")] public float floatThreshold = 2f;
    [ConditionalHide("boyanceSimulate")] public float waterDensity = .8f;
    [ConditionalHide("boyanceSimulate")] public float downForce = .5f;

    [Header("Generate More After Start")]
    public bool generateMore = true;
    [ConditionalHide("generateMore")] public int generateCounter = 16;

    static private bool DEBUG_MODE = false;
    [System.Obsolete]
    private bool isModified = false;

    #endregion

    #region Helper Functions
    Vector3 RandomizeVector3(Vector3 min, Vector3 max) {
        float rand_x = Random.Range(min.x, max.x);
        float rand_y = Random.Range(min.y, max.y);
        float rand_z = Random.Range(min.z, max.z);
        return new Vector3(rand_x, rand_y, rand_z);
    }
    
    bool IsBetween(float point, float point1, float point2) {
    // given two point without knowing which one is greater, see if point given is in between 
        bool result = (Math.Abs(point1 - point2) >= Math.Abs(point - point1) && Math.Abs(point1 - point2) >= Math.Abs(point - point2));
        if (DEBUG_MODE)
            Debug.Log("IsBetween(point=" + point + ", point1=" + point1 + ", point2=" + point2 + ") returns " + result);
        
        return result; 
    }
    
    Vector3 RandomizeVector3Except(Vector3 smallRangeMin, Vector3 smallRangeMax, Vector3 bigRangeMin, Vector3 bigRangeMax) {
    // input two areas: small and big areas defined by their min max bounds
    // output a random vector3 that falls in the bigger area but not the smaller area
    // bigRange must wrap around than smallRange

        Vector3 randVector = RandomizeVector3(bigRangeMin, bigRangeMax);
        if (DEBUG_MODE)
            Debug.Log("RandomizeExcept: returns randVector " + randVector);
        if (IsBetween(randVector.x, smallRangeMax.x, smallRangeMin.x)) {
            while (IsBetween(randVector.z, smallRangeMin.z, smallRangeMax.z))
                randVector.z = Random.Range(bigRangeMin.z, bigRangeMax.z);
        }
        return randVector;    
    } 

    Vector3 GetBoundMinOf(GameObject obj) {
        Renderer rend = obj.GetComponent<Renderer>();
        if (DEBUG_MODE)
            Debug.Log(obj + " has min bound: " + rend.bounds.min);
        return rend.bounds.min;
    }

    Vector3 GetBoundMaxOf(GameObject obj) {
        Renderer rend = obj.GetComponent<Renderer>();
        if (DEBUG_MODE)
            Debug.Log(obj + " has max bound: " + rend.bounds.max);
        return rend.bounds.max;
    }

    public void GenerateMore(Metadata.trash input, bool isInteractive) {
        GenerateGameObject(input, isInteractive = isSmallZoneActive, true);
    }

    #endregion

    #region Generate Object Function
    List<List<GameObject>> GenerateGameObject(Metadata.trash trashShape, bool isInteractive, bool smallAreaOnly) {
        // List<gObj> is the number of prefabs generated by ONE scriptable Objects
        // List<List<>> is list of scriptable objects (e.g. all the scriptable sphere objs)

        List<List<GameObject>> objectLists = new List<List<GameObject>>();

        for (int i = 0; i < Metadata.sizeList.Count; i++) {
            objectLists.Add(new List<GameObject>());

            #region Create multiple object lists

            // interactive objs are in active zone else outside of active zone
            Vector3 minBorder, maxBorder;
            int count;
            if (isInteractive)
                count = interactiveCount;
            else
                count = nonInteractiveCount;


            for (int objectCounter = 0; objectCounter < count; objectCounter++) {
                #region Create Objects
                // create the primitive shape
                GameObject current = GameObject.CreatePrimitive(Metadata.trashType[trashShape]) as GameObject;
                
                // Randomize position
                Vector3 tempVec;
                if (smallAreaOnly) {
                    minBorder = GetBoundMinOf(smallZone);
                    maxBorder = GetBoundMaxOf(smallZone);
                    tempVec = RandomizeVector3(minBorder, maxBorder);
                }
                else {
                    minBorder = GetBoundMinOf(wrappedZone);
                    maxBorder = GetBoundMaxOf(wrappedZone);
                    tempVec = RandomizeVector3Except(GetBoundMinOf(smallZone), GetBoundMaxOf(smallZone), minBorder, maxBorder);
                }
                
                Vector3 newPosition = new Vector3(tempVec.x, Random.Range(transform.position.y, transform.position.y + RandomHeightMax), tempVec.z);

                // Transform
                current.transform.position = newPosition;
                current.transform.rotation = Random.rotationUniform;

                if (DEBUG_MODE)
                    if (!isInteractive) Debug.Log("Position of NonInteractive Object: " + newPosition);

                // Add Display Script to the Scriptable object
                DisplayScript displayScript;
                if (trashShape == Metadata.trash.cylinder)
                    displayScript = current.AddComponent<CylinderDisplay>() as CylinderDisplay;
                else
                    displayScript = current.AddComponent<CubeDisplay>() as CubeDisplay;
                
                displayScript.scriptObject = ScriptableAssetManager.LoadAsset(trashShape, sizeList[i], isInteractive) as Shape;

                // Add Floating script if boyanceSimulate is true
                if (boyanceSimulate) {
                    ObjectFloat flooaty = current.AddComponent<ObjectFloat>() as ObjectFloat;
                    flooaty.waterLevel = waterLevel;
                    flooaty.floatThreshold = floatThreshold;
                    flooaty.waterDensity = waterDensity;
                    flooaty.downForce = downForce;
                }

                // Add Force script if addForceToObject is true
                if (addForceToObject) {
                    ObjectForce forcey = current.AddComponent<ObjectForce>() as ObjectForce;
                    forcey.upForce = upForce;
                    forcey.sideForce = sideForce;
                }
                #endregion
                current.tag = "trash";
                objectLists[i].Add(current);
            }

        }
        #endregion

        return objectLists;
    }

    private int GLOBAL_COUNTER = 0;

    private void GenerateMoreRandomly(int input) {
        if (Random.Range(-1f, 1f) > 0) {
            if (GLOBAL_COUNTER < input) {
                if (DEBUG_MODE)
                    Debug.Log("counter: " + GLOBAL_COUNTER);
                if (GLOBAL_COUNTER % 3 == 0) GenerateMore(Metadata.trash.cylinder, false);
                else if (GLOBAL_COUNTER % 3 == 1) GenerateMore(Metadata.trash.cube, false);
                else if (GLOBAL_COUNTER % 3 == 2) GenerateMore(Metadata.trash.sphere, false);
                GLOBAL_COUNTER++;
            }
        }
    }
    #endregion


    void Start() {
        bool isInteractive;
        bool overrideAsset = false;
        // Caution: Should only use this to generate the asset at the beginning, never at run-time!!  
        // Because the assets will be overrided if there are multiple Generate Object scripts on the scene
        // leads to run-time error
        //
        if (isWrappedZoneActive || isSmallZoneActive)
            ScriptableAssetManager.CreateAllAssets(sizeList, isInteractive = true, overrideAsset);
        if (!isWrappedZoneActive || !isSmallZoneActive)
            ScriptableAssetManager.CreateAllAssets(sizeList, isInteractive = false, overrideAsset);

        if (wrappedZone != null) {
            if (generateCube) GenerateGameObject(Metadata.trash.cube, isInteractive = isWrappedZoneActive, false);
            if (generateSphere) GenerateGameObject(Metadata.trash.sphere, isInteractive = isWrappedZoneActive, false);
            if (generateCylinder) GenerateGameObject(Metadata.trash.cylinder, isInteractive = isWrappedZoneActive, false);
        }

        if (smallZone != null) {
            if (generateCube) GenerateGameObject(Metadata.trash.cube, isInteractive = isSmallZoneActive, true);
            if (generateSphere) GenerateGameObject(Metadata.trash.sphere, isInteractive = isSmallZoneActive, true);
            if (generateCylinder) GenerateGameObject(Metadata.trash.cylinder, isInteractive = isSmallZoneActive, true);
            

        }
    }

    [System.Obsolete]
    private GameObject DebugGameObject(List<List<GameObject>> debug) {
        int debugIndex = debug.Capacity - 1;
        List<GameObject> objectList = debug[debugIndex];
        int objIndex = objectList.Capacity - 3;
        GameObject obj = objectList[objIndex];
        return obj;
    }

    private void FixedUpdate() {
    }

    private void Update() {
        if (generateMore)
            GenerateMoreRandomly(generateCounter);
    }
}