using GestorOT.Domain.Enums;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Las personas del ERP se activan solas y el rol sale del "grupo de personas" de
/// GestorMax. El filtro propio/contratista de labores y OT se apoya en ese rol.
/// </summary>
public class ContactRoleModeTests
{
    [Theory]
    [InlineData("CONTRATISTAS", ContactRole.Contractor)]
    [InlineData("Terceros servicios", ContactRole.Contractor)]
    [InlineData("PROVEEDORES", ContactRole.Supplier)]
    [InlineData("EMPLEADOS", ContactRole.InternalStaff)]
    [InlineData("Personal de campo", ContactRole.InternalStaff)]
    [InlineData("CLIENTES", ContactRole.Unclassified)]
    [InlineData("", ContactRole.Unclassified)]
    [InlineData(null, ContactRole.Unclassified)]
    public void InferFromErpGroup_MapsGroupToRole(string? group, ContactRole expected)
    {
        Assert.Equal(expected, ContactRoleExtensions.InferFromErpGroup(group));
    }

    [Fact]
    public void ToExecutionMode_ContractorsAndSuppliersAreContractorStaffIsOwn_UnclassifiedIsBoth()
    {
        Assert.Equal(LaborExecutionMode.Contractor, ContactRole.Contractor.ToExecutionMode());
        Assert.Equal(LaborExecutionMode.Contractor, ContactRole.Supplier.ToExecutionMode());
        Assert.Equal(LaborExecutionMode.Own, ContactRole.InternalStaff.ToExecutionMode());
        Assert.Equal(LaborExecutionMode.Own, ContactRole.Agronomist.ToExecutionMode());
        Assert.Null(ContactRole.Unclassified.ToExecutionMode());

        // Sin clasificar se ofrece en los dos modos.
        Assert.True(ContactRole.Unclassified.ToExecutionMode().MatchesFilter(LaborExecutionMode.Own));
        Assert.True(ContactRole.Unclassified.ToExecutionMode().MatchesFilter(LaborExecutionMode.Contractor));
        Assert.False(ContactRole.InternalStaff.ToExecutionMode().MatchesFilter(LaborExecutionMode.Contractor));
        Assert.False(ContactRole.Contractor.ToExecutionMode().MatchesFilter(LaborExecutionMode.Own));
    }
}
