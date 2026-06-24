import type { MainCategory } from './enums'

// GET /api/v1/categories → IReadOnlyList<CategoryDto>
// Estructura ANIDADA: cada categoría principal (enum MainCategory) tiene sus subcategorías.
// Esto alimenta los combos anidados Categoría → Subcategoría en los formularios de transacción.

export interface SubCategory {
  idSubCategory: number
  name: string
  isDefault: boolean
}

export interface Category {
  idMainCategory: MainCategory
  name: string
  subCategories: SubCategory[]
}
