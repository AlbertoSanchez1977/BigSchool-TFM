// GET /api/v1/categories → IReadOnlyList<CategoryDto>
// Estructura ANIDADA: cada categoría principal tiene sus subcategorías.
//
// IMPORTANTE: CategoryDto.IdMainCategory es int en el backend (Dapper lee número),
// pero CategoryDto.Name es mc.ToString() → coincide con el MainCategory string union.
// Para el form usamos category.name (string) como valor de los selects.

export interface SubCategory {
  idSubCategory: number
  name: string
  isDefault: boolean
}

export interface Category {
  idMainCategory: number  // int numérico del backend (1=EssentialExpenses, 10=Salary, etc.)
  name: string            // mc.ToString() → mismo valor que MainCategory ('EssentialExpenses', ...)
  subCategories: SubCategory[]
}
