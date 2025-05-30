using Wasm2cs.Runtime;

namespace Wasm2cs.Autogenerated;


class HelloWorld
{
    private readonly Action<Int32> _import_sayc;

    #region Instantiation
    public static HelloWorld Instantiate(Action<Int32> sayc)
    {
        return new HelloWorld(sayc);
    }

    private HelloWorld(Action<Int32> sayc)
    {
        _import_sayc = sayc;
    }
    #endregion


    private readonly Memory _memory_memory = new Memory(2, 4294967295);
    public Memory memory => _memory_memory;


    private System.Int32 backing___heap_base = InitGlobal___heap_base();
    public System.Int32 __heap_base
    {
        private set => backing___heap_base = value;
        get => backing___heap_base;
    }

    private static System.Int32 InitGlobal___heap_base()
    {
        return 66560;
    }


    private System.Int32 backing___data_end = InitGlobal___data_end();
    public System.Int32 __data_end
    {
        private set => backing___data_end = value;
        get => backing___data_end;
    }

    private static System.Int32 InitGlobal___data_end()
    {
        return 1024;
    }


    public Int32 main()
    {
        return Function_2();
    }


    private void Function_0(System.Int32 _param0)
    {
         _import_sayc(_param0);
    }


    private void Function_1()
    {
    }


    private Int32 Function_2()
    {
        const System.Int32 stack0 = (72);
        Function_0(stack0);
        const System.Int32 stack1 = (101);
        Function_0(stack1);
        const System.Int32 stack2 = (108);
        Function_0(stack2);
        const System.Int32 stack3 = (108);
        Function_0(stack3);
        const System.Int32 stack4 = (111);
        Function_0(stack4);
        const System.Int32 stack5 = (32);
        Function_0(stack5);
        const System.Int32 stack6 = (87);
        Function_0(stack6);
        const System.Int32 stack7 = (111);
        Function_0(stack7);
        const System.Int32 stack8 = (114);
        Function_0(stack8);
        const System.Int32 stack9 = (108);
        Function_0(stack9);
        const System.Int32 stack10 = (100);
        Function_0(stack10);
        const System.Int32 stack11 = (32);
        Function_0(stack11);
        const System.Int32 stack12 = (40);
        Function_0(stack12);
        const System.Int32 stack13 = (102);
        Function_0(stack13);
        const System.Int32 stack14 = (114);
        Function_0(stack14);
        const System.Int32 stack15 = (111);
        Function_0(stack15);
        const System.Int32 stack16 = (109);
        Function_0(stack16);
        const System.Int32 stack17 = (32);
        Function_0(stack17);
        const System.Int32 stack18 = (87);
        Function_0(stack18);
        const System.Int32 stack19 = (65);
        Function_0(stack19);
        const System.Int32 stack20 = (83);
        Function_0(stack20);
        const System.Int32 stack21 = (77);
        Function_0(stack21);
        const System.Int32 stack22 = (41);
        Function_0(stack22);
        const System.Int32 stack23 = (10);
        Function_0(stack23);
        const System.Int32 stack24 = (0);
        return (stack24);
    }


}
