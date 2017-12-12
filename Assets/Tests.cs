﻿using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using PlayQ.UITestTools;
using UnityEngine;

public class Tests //: UITest
{
    [SetUp]
    public void Init()
    {
        Debug.Log("SET UP");
    }

    [TearDown]
    public void TearDown()
    {
        Debug.Log("TEAR DOWN");
    }

    [Test]
    [Ignore("IGNORE BECASE")]
    public int Test()
    {
        Debug.Log("IGNORED TEST");
        return 1;
    }

    [Test]
    public int ReturnInt()
    {
        Debug.Log("IGNORED TEST");
        return 1;
    }

    [UnityTest]
    public IEnumerable ThrowException()
    {
        yield return new WaitForSeconds(1f);
        throw new Exception("EXCEPTION");
    }

    [UnityTest]
    [Timeout(5000)]
    public IEnumerable TimeOut()
    {
        yield return new WaitForSeconds(10f);
    }

    [UnityTest]
    public IEnumerator LogError()
    {
        yield return new WaitForSeconds(1f);
        yield return new WaitForSeconds(1f);
        Debug.LogError("LOG ERROR");
        yield return new WaitForSeconds(1f);
        Debug.Log("lol");
    }

    [Test]
    public void Log()
    {
        Debug.Log("LOG some log");
    }

    [UnityTest]
    public IEnumerator Success()
    {
        Debug.Log("1.1");
        yield return new WaitForSeconds(1f);
        Debug.Log("1.2");
        yield return new WaitForSeconds(1f);
        Debug.Log("1.3");
        yield return new WaitForSeconds(1f);
        Debug.Log("1.4");
    }

    [UnityTest]
    public IEnumerable Success2()
    {
        Debug.Log("2.1");
        yield return new WaitForSeconds(1f);
        Debug.Log("2.2");
        yield return new WaitForSeconds(1f);
        Debug.Log("2.3");
        yield return new WaitForSeconds(1f);
        Debug.Log("2.4");
    }

    [Test]
    public void TestProtected()
    {
        var type = typeof(B);

        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
        foreach (var method in methods)
        {

        }
    }
}



public class A
{
    protected void ProtectedA()
    {
        
    }
    
    private void PrivateA()
    {
        
    }
    
    public void PublicA()
    {
        
    }
}

public class B : A
{
    protected void ProtectedB()
    {
        
    }
    
    private void PrivateB()
    {
        
    }
    
    public void PublicB()
    {
        
    }
}