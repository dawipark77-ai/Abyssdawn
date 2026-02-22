import json
import os

def export_skills():
    input_path = 'StoneshardSkillReference.json'
    output_path = 'StoneshardSkills_Export.txt'

    if not os.path.exists(input_path):
        print(f"Error: {input_path} not found.")
        return

    try:
        with open(input_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
    except Exception as e:
        print(f"Error reading JSON: {e}")
        return

    output_lines = []
    
    # Header
    output_lines.append(f"Stoneshard Skill Reference Export")
    output_lines.append(f"Source: {data.get('source', 'Unknown')}")
    output_lines.append(f"Description: {data.get('description', '')}")
    output_lines.append("="*50 + "\n")

    skill_trees = data.get('skillTrees', {})

    for tree_name, tree_data in skill_trees.items():
        output_lines.append(f"[[ {tree_name} ]]")
        
        focus = ", ".join(tree_data.get('mainFocus', []))
        output_lines.append(f"Main Focus: {focus}")
        output_lines.append(f"Crit Effect: {tree_data.get('critEffect', 'None')}")
        output_lines.append("-" * 30 + "\n")

        skills_by_treatise = tree_data.get('skills', {})
        
        # Sort treatises just in case (Treatise I, II, III, IV)
        # Assuming keys are "Treatise I", "Treatise II", etc.
        sorted_treatises = sorted(skills_by_treatise.keys())

        for treatise in sorted_treatises:
            output_lines.append(f"--- {treatise} ---")
            skills = skills_by_treatise[treatise]
            
            for skill in skills:
                name = skill.get('name', 'Unnamed Skill')
                s_type = skill.get('type', 'Unknown Type')
                
                output_lines.append(f"\n> {name} ({s_type})")
                
                attack_type = skill.get('attackType')
                if attack_type:
                    output_lines.append(f"  Target: {attack_type}")
                
                range_val = skill.get('range')
                if range_val:
                    output_lines.append(f"  Range: {range_val}")
                    
                energy = skill.get('energy')
                cooldown = skill.get('cooldown')
                cost_str = []
                if energy: cost_str.append(f"Energy: {energy}")
                if cooldown: cost_str.append(f"Cooldown: {cooldown}")
                if cost_str:
                    output_lines.append(f"  Cost: {', '.join(cost_str)}")
                
                req = skill.get('requirement')
                if req:
                    output_lines.append(f"  Requirement: {req}")

                effects = skill.get('effects', [])
                if effects:
                    output_lines.append("  Effects:")
                    for effect in effects:
                        output_lines.append(f"  - {effect}")
            
            output_lines.append("") # Empty line between treatises
        
        output_lines.append("="*50 + "\n") # Separator between trees

    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("\n".join(output_lines))
        print(f"Successfully exported to {output_path}")
    except Exception as e:
        print(f"Error writing output: {e}")

if __name__ == "__main__":
    export_skills()
