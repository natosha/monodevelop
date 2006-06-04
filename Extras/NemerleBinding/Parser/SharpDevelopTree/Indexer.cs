// created on 06.08.2003 at 12:34

using MonoDevelop.Projects.Parser;
using Nemerle.Completion;
using SR = System.Reflection;
using NCC = Nemerle.Compiler;
using Nemerle.Compiler.Typedtree;

using System.Xml;

namespace NemerleBinding.Parser.SharpDevelopTree
{
	public class Indexer : AbstractIndexer
	{
	    XmlNode node;
	
		public void AddModifier(ModifierEnum m)
		{
			modifiers = modifiers | m;
		}
		
		void LoadXml (Class declaring)
        {
			if (declaring.xmlHelp != null) {
				node = declaring.xmlHelp.SelectSingleNode ("/Type/Members/Member[@MemberName='" + FullyQualifiedName + "']");
				if (node != null) {
					XmlNode docNode = node.SelectSingleNode ("Docs/summary");
					if (docNode != null) {
						Documentation = node.InnerXml;
					}
				}
			}
        }
		
		internal Method Getter;
		internal Method Setter;
		
		public Indexer (Class declaringType, SR.PropertyInfo tinfo)
		{
		    this.declaringType = declaringType;
		
		    ModifierEnum mod = (ModifierEnum)0;
			modifiers = mod;
			
			this.FullyQualifiedName = tinfo.Name;
			returnType = new ReturnType(tinfo.PropertyType);
			this.region = Class.GetRegion();
			this.bodyRegion = Class.GetRegion();
			
			LoadXml (declaringType);
			    
			// Add parameters
			foreach (SR.ParameterInfo pinfo in tinfo.GetIndexParameters())
			    parameters.Add(new Parameter(this, pinfo, node));
		}
		
		public Indexer (Class declaringType, NCC.IProperty tinfo)
		{
		    this.declaringType = declaringType;
		
		    ModifierEnum mod = (ModifierEnum)0;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Private) != 0)
                mod |= ModifierEnum.Private;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Internal) != 0)
                mod |= ModifierEnum.Internal;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Protected) != 0)
                mod |= ModifierEnum.Protected;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Public) != 0)
                mod |= ModifierEnum.Public;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Abstract) != 0)
                mod |= ModifierEnum.Abstract;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Sealed) != 0)
                mod |= ModifierEnum.Sealed;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Static) != 0)
                mod |= ModifierEnum.Static;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Override) != 0)
                mod |= ModifierEnum.Override;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Virtual) != 0)
                mod |= ModifierEnum.Virtual;
            if ((tinfo.Attributes & NCC.NemerleAttributes.New) != 0)
                mod |= ModifierEnum.New;
            if ((tinfo.Attributes & NCC.NemerleAttributes.Extern) != 0)
                mod |= ModifierEnum.Extern;
                
			modifiers = mod;
			
			this.FullyQualifiedName = tinfo.Name;
			returnType = new ReturnType (tinfo.GetMemType ());
			this.region = Class.GetRegion (tinfo.Location);
            if (tinfo is NCC.MemberBuilder)
                this.bodyRegion = Class.GetRegion (((NCC.MemberBuilder)tinfo).BodyLocation);
            else
                this.bodyRegion = Class.GetRegion (tinfo.Location);
			
			NCC.IMethod getter = tinfo.GetGetter ();
			NCC.IMethod setter = tinfo.GetSetter ();
			if (getter != null)
		    {
			    this.Getter = new Method(declaringType, getter);
			    if (getter is NCC.MemberBuilder)
			        getterRegion = Class.GetRegion (((NCC.MemberBuilder)getter).BodyLocation);
			    else
			       getterRegion = Class.GetRegion(getter.Location);
			}
			if (setter != null)
			{
			    this.Setter = new Method(declaringType, setter);
			    if (setter is NCC.MemberBuilder)
			        setterRegion = Class.GetRegion (((NCC.MemberBuilder)setter).BodyLocation);
			    else
			        setterRegion = Class.GetRegion(setter.Location);
			}
			
			LoadXml (declaringType);
			    
			// Add parameters
			if (getter != null)
			{
			    foreach (Fun_parm pinfo in getter.GetParameters ())
			       parameters.Add(new Parameter(this, pinfo, node));
			}
		}
		
		public new IRegion GetterRegion {
			get { return getterRegion; }
			set { getterRegion = value; }
		}

		public new IRegion SetterRegion {
			get { return setterRegion; }
			set { setterRegion = value; }
		}
	}
}
