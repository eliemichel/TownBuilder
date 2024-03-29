﻿using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class MainTest
    {
        [Test]
        public void RunTestBMesh()
        {
            TestBMesh.Run();
        }

        [Test]
        public void RunTestAxialCoordinate()
        {
            TestAxialCoordinate.Run();
        }

        [Test]
        public void RunTestBMeshOperators()
        {
            TestBMeshOperators.Run();
        }
    }
}
