# PersistenceCollections
<b>PersistenceList</b> &amp; <b>PersistenceDictionary</b>, are <i>Persistent Collections</i> for massive get/insert of data without use of memory to iterate objects (be carefull, because using the hard disk); working with compression LZ4, SQLite &amp; DynamicProxy with several serializers & it's compatible with the normaliced use of .NET List, Dictionary & Interfaces plus as Sorted/Ordered; also supports custom serializers & compressors (you can implement JSON, LZO, etc).
<br/><br/>
This is designed for long inserts to DB, fixing the exceding the 2gb memory in debug & Release restrictions too; long process that you may lost the memory data, or requeries the maxium memory avaliable.
<br/><br/>
Now, this prototype solution has a fully functional stable core; In a future I will add more documentation and examples with Unit Testing.
