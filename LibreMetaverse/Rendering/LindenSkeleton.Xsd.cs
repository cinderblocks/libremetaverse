namespace OpenMetaverse.Rendering
{
    using System;
    using System.Xml.Serialization;

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [XmlRoot("linden_skeleton", Namespace = "", IsNullable = false)]
    public partial class LindenSkeleton
    {

        private Joint boneField = new Joint();

        private float versionField;

        private bool versionFieldSpecified;

        private string num_bonesField = string.Empty;

        private string num_collision_volumesField = string.Empty;

        /// <remarks/>
        public Joint bone
        {
            get
            {
                return this.boneField;
            }
            set
            {
                this.boneField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute()]
        public float version
        {
            get
            {
                return this.versionField;
            }
            set
            {
                this.versionField = value;
            }
        }

        /// <remarks/>
        [XmlIgnore()]
        public bool versionSpecified
        {
            get
            {
                return this.versionFieldSpecified;
            }
            set
            {
                this.versionFieldSpecified = value;
            }
        }

        /// <remarks/>
        [XmlAttribute(DataType = "positiveInteger")]
        public string num_bones
        {
            get
            {
                return this.num_bonesField;
            }
            set
            {
                this.num_bonesField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute(DataType = "positiveInteger")]
        public string num_collision_volumes
        {
            get
            {
                return this.num_collision_volumesField;
            }
            set
            {
                this.num_collision_volumesField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class Joint : JointBase
    {

        private CollisionVolume[] collision_volumeField = Array.Empty<CollisionVolume>();

        private Joint[] boneField = Array.Empty<Joint>();

        private float[] pivotField = Array.Empty<float>();

        private string aliasesField = string.Empty;

        private bool connectedField;

        /// <remarks/>
        [XmlElement("collision_volume")]
        public CollisionVolume[] collision_volume
        {
            get
            {
                return this.collision_volumeField;
            }
            set
            {
                this.collision_volumeField = value;
            }
        }

        /// <remarks/>
        [XmlElement("bone")]
        public Joint[] bone
        {
            get
            {
                return this.boneField;
            }
            set
            {
                this.boneField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute()]
        public float[] pivot
        {
            get { return this.pivotField; }
            set { this.pivotField = value; }
        }

        /// <summary>Space-separated list of alias names for this bone (e.g. "hip avatar_mPelvis").</summary>
        [XmlAttribute(DataType = "token")]
        public string aliases
        {
            get { return this.aliasesField; }
            set { this.aliasesField = value; }
        }

        /// <summary>True when this bone is connected to its parent in a continuous chain.</summary>
        [XmlAttribute()]
        public bool connected
        {
            get { return this.connectedField; }
            set { this.connectedField = value; }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class CollisionVolume : JointBase
    {
    }

    /// <remarks/>
    [XmlInclude(typeof(Joint))]
    [XmlInclude(typeof(CollisionVolume))]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class JointBase
    {

        private string nameField = string.Empty;

        private float[] posField = Array.Empty<float>();

        private float[] rotField = Array.Empty<float>();

        private float[] scaleField = Array.Empty<float>();

        private string groupField = string.Empty;

        private string supportField = string.Empty;

        private float[] endField = Array.Empty<float>();

        private bool repositionField;

        /// <remarks/>
        [XmlAttribute(DataType = "token")]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute()]
        public float[] pos
        {
            get
            {
                return this.posField;
            }
            set
            {
                this.posField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute()]
        public float[] rot
        {
            get
            {
                return this.rotField;
            }
            set
            {
                this.rotField = value;
            }
        }

        /// <remarks/>
        [XmlAttribute()]
        public float[] scale
        {
            get { return this.scaleField; }
            set { this.scaleField = value; }
        }

        /// <summary>Bone group for organisational purposes, e.g. "Torso", "Spine", "Left Arm".</summary>
        [XmlAttribute(DataType = "token")]
        public string group
        {
            get { return this.groupField; }
            set { this.groupField = value; }
        }

        /// <summary>Support level: "base" for original LL bones, "extended" for fitted-mesh / creature bones.</summary>
        [XmlAttribute(DataType = "token")]
        public string support
        {
            get { return this.supportField; }
            set { this.supportField = value; }
        }

        /// <summary>Endpoint of the bone in parent-local space — used by external tools and diagnostic display.</summary>
        [XmlAttribute()]
        public float[] end
        {
            get { return this.endField; }
            set { this.endField = value; }
        }

        /// <summary>True when this joint supports joint-position overrides from rigged mesh attachments.</summary>
        [XmlAttribute()]
        public bool reposition
        {
            get { return this.repositionField; }
            set { this.repositionField = value; }
        }
    }
}
